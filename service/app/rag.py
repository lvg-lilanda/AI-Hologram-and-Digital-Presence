"""Retrieval over the approved corpus, with citations.

T15 acceptance criteria:
  - "Approved documents can be indexed with one documented command."
        python -m app.cli index
  - "Retrieved passages include source title and section or page."
        every chunk carries title + section (markdown heading) or page (PDF).
  - "Unsupported questions return an explicit not-found response."
        best cosine score below AIHOLO_GROUNDING_FLOOR -> grounded=False, and the
        avatar says so instead of inventing an answer.

The index is a single .npz plus a .json sidecar. No vector database: the corpus is
a handful of approved documents, it has to ship to a lab PC with no admin rights,
and a numpy dot product over a few thousand chunks is well under a millisecond.
"""
from __future__ import annotations

import json
import re
from dataclasses import asdict, dataclass
from datetime import datetime
from pathlib import Path

import httpx

from .config import INDEX_DIR, Settings

EMBED_BATCH = 32


class EmbeddingUnavailable(RuntimeError):
    """Ollama is not serving the embedding model. Surfaced as INDEX_EMPTY so the
    operator sees which thing to start, rather than a stack trace."""


CHUNK_CHARS = 900
CHUNK_OVERLAP = 150
INDEX_VECTORS = INDEX_DIR / "vectors.npz"
INDEX_CHUNKS = INDEX_DIR / "chunks.json"
INDEX_META = INDEX_DIR / "meta.json"


INDEX_SCHEMA = 2


@dataclass
class Chunk:
    text: str
    title: str
    section: str = ""
    page: int | None = None
    source: str = ""
    #: True when this passage sits under a heading that declares something the
    #: avatar does not handle. Retrieving one is a refusal, not an answer.
    out_of_scope: bool = False


def _split(text: str) -> list[str]:
    text = re.sub(r"\n{3,}", "\n\n", text).strip()
    if len(text) <= CHUNK_CHARS:
        return [text] if text else []
    out, start = [], 0
    while start < len(text):
        end = min(start + CHUNK_CHARS, len(text))
        if end < len(text):
            brk = text.rfind(". ", start + CHUNK_CHARS // 2, end)
            if brk != -1:
                end = brk + 1
        piece = text[start:end].strip()
        if piece:
            out.append(piece)
        if end >= len(text):
            break
        start = max(end - CHUNK_OVERLAP, start + 1)
    return out


def _read_markdown(path: Path, out_of_scope_headings: tuple[str, ...] = ()) -> list[Chunk]:
    """Split on headings so `section` is the real heading a reader would cite."""
    raw = path.read_text(encoding="utf-8", errors="replace")
    title, section, buf, chunks = path.stem, "", [], []

    def flush():
        oos = section.strip().lower() in out_of_scope_headings
        for piece in _split("\n".join(buf)):
            chunks.append(Chunk(text=piece, title=title, section=section,
                                source=path.name, out_of_scope=oos))
        buf.clear()

    for line in raw.splitlines():
        m = re.match(r"^(#{1,6})\s+(.*)$", line)
        if m:
            flush()
            heading = m.group(2).strip()
            if m.group(1) == "#" and title == path.stem:
                title = heading
            section = heading
        else:
            buf.append(line)
    flush()
    return chunks


def _read_pdf(path: Path) -> list[Chunk]:
    from pypdf import PdfReader

    reader = PdfReader(str(path))
    meta_title = (reader.metadata.title if reader.metadata else None) or path.stem
    chunks: list[Chunk] = []
    for page_no, page in enumerate(reader.pages, start=1):
        for piece in _split(page.extract_text() or ""):
            chunks.append(
                Chunk(text=piece, title=meta_title, page=page_no, source=path.name)
            )
    return chunks


def _read_text(path: Path) -> list[Chunk]:
    raw = path.read_text(encoding="utf-8", errors="replace")
    return [Chunk(text=p, title=path.stem, source=path.name) for p in _split(raw)]


READERS = {".md": _read_markdown, ".markdown": _read_markdown, ".pdf": _read_pdf,
           ".txt": _read_text}
MARKDOWN_SUFFIXES = {".md", ".markdown"}


def load_corpus(corpus_dir: Path, out_of_scope_headings: tuple[str, ...] = ()) -> list[Chunk]:
    chunks: list[Chunk] = []
    for path in sorted(corpus_dir.rglob("*")):
        if path.name.lower() == "readme.md" or not path.is_file():
            continue
        suffix = path.suffix.lower()
        reader = READERS.get(suffix)
        if reader is None:
            continue
        if suffix in MARKDOWN_SUFFIXES:
            chunks.extend(reader(path, out_of_scope_headings))
        else:
            chunks.extend(reader(path))
    return chunks


#: Removed before matching. Without this, "who won the grand final?" overlaps any
#: passage containing "the" and reads as grounded.
STOPWORDS = frozenset("""a an and are as at be been but by can could did do does for
from had has have how i if in is it its me my of on or our so than that the their them
then there these they this to was we were what when where which who whom why will with
would you your""".split())


def content_words(text: str) -> set[str]:
    return {w for w in re.findall(r"[a-z0-9']+", text.lower())
            if w not in STOPWORDS and len(w) > 1}


def coverage(question: str, chunk_text: str) -> float:
    """Share of the question's content words that appear in the passage.

    This is the MOCK-mode matcher only. Hashed bag-of-words vectors were tried first
    and measured: across five answerable and five unanswerable questions the cosine
    scores overlapped completely (0.162 appeared in both sets), so no floor could
    separate them and the service would have claimed to know things it did not.
    Term coverage separates the same ten cleanly. It is a stand-in for MiniLM, not a
    rival to it - real retrieval quality is decided on the VX PC against Anuji's
    20-question set (T08), not here.
    """
    q = content_words(question)
    if not q:
        return 0.0
    return len(q & content_words(chunk_text)) / len(q)


class Retriever:
    def __init__(self, cfg: Settings):
        self._cfg = cfg
        self._encoder = None
        self._vectors = None
        self._chunks: list[Chunk] = []
        self._error = ""
        self._loaded = False

    # -- encoder -------------------------------------------------------------
    def _encode(self, texts: list[str], kind: str = "document"):
        """Embed via Ollama rather than sentence-transformers.

        sentence-transformers drags in torch - about 1.5 GB installed. Ollama is
        already the runtime this service depends on, and nomic-embed-text is a
        274 MB pull, so using it for embeddings too removes torch from the
        dependency tree entirely. That matters twice over: a smaller install on
        the Mac now, and a much smaller no-admin portable bundle for the VX PC.

        nomic-embed-text is asymmetric - it wants the passage and the question
        prefixed differently, or retrieval quality drops noticeably.
        """
        import numpy as np

        prefix = "search_query: " if kind == "query" else "search_document: "
        vectors: list[list[float]] = []
        for start in range(0, len(texts), EMBED_BATCH):
            batch = [prefix + t for t in texts[start:start + EMBED_BATCH]]
            try:
                r = httpx.post(
                    f"{self._cfg.ollama_url}/api/embed",
                    json={"model": self._cfg.embed_model, "input": batch},
                    timeout=self._cfg.embed_timeout_s,
                )
                r.raise_for_status()
                vectors.extend(r.json()["embeddings"])
            except Exception as exc:
                raise EmbeddingUnavailable(
                    f"Ollama embeddings failed ({self._cfg.embed_model} at "
                    f"{self._cfg.ollama_url}): {exc}"
                ) from exc

        arr = np.asarray(vectors, dtype="float32")
        norms = np.linalg.norm(arr, axis=1, keepdims=True)
        norms[norms == 0] = 1.0
        return arr / norms

    # -- build ---------------------------------------------------------------
    def build(self, corpus_dir: Path) -> int:
        import numpy as np

        chunks = load_corpus(corpus_dir, self._cfg.out_of_scope_headings)
        if not chunks:
            raise ValueError(
                f"No indexable documents under {corpus_dir}. "
                "Add approved .md, .txt or .pdf files first."
            )
        # Mock mode stores a placeholder instead of embeddings: it matches
        # lexically, and not importing sentence-transformers keeps the mock service
        # installable without torch, so Unity work never waits on a 2 GB download.
        vectors = (np.zeros((len(chunks), 1), dtype="float32") if self._cfg.mock
                   else self._encode([c.text for c in chunks]))
        INDEX_DIR.mkdir(parents=True, exist_ok=True)
        np.savez_compressed(INDEX_VECTORS, vectors=vectors)
        INDEX_CHUNKS.write_text(
            json.dumps([asdict(c) for c in chunks], ensure_ascii=False), encoding="utf-8"
        )
        INDEX_META.write_text(json.dumps({
            "schema": INDEX_SCHEMA,
            "embedder": self._embedder_id(),
            "chunks": len(chunks),
            "built": datetime.now().isoformat(timespec="seconds"),
        }), encoding="utf-8")
        self._vectors, self._chunks, self._loaded = vectors, chunks, True
        return len(chunks)

    def _embedder_id(self) -> str:
        return "lexical(mock)" if self._cfg.mock else self._cfg.embed_model

    # -- load ----------------------------------------------------------------
    def _load(self):
        if self._loaded or self._error:
            return
        if not (INDEX_VECTORS.exists() and INDEX_CHUNKS.exists()):
            self._error = "index not built - run: python -m app.cli index"
            return

        # An index built by a different embedder loads without complaint and then
        # retrieves nonsense - mock builds store placeholder vectors, and two real
        # embedding models produce incompatible vector spaces. Silent bad retrieval
        # is the worst failure this service has, so it is refused loudly instead.
        built_by, schema = "unknown (pre-dating this check)", 0
        if INDEX_META.exists():
            try:
                meta = json.loads(INDEX_META.read_text(encoding="utf-8"))
                built_by, schema = meta["embedder"], meta.get("schema", 1)
            except (json.JSONDecodeError, KeyError):
                pass
        if schema != INDEX_SCHEMA:
            self._error = (
                f"index uses schema {schema}, this build expects {INDEX_SCHEMA} "
                "(out-of-scope marking was added). Rebuild it: python -m app.cli index"
            )
            return
        if built_by != self._embedder_id():
            self._error = (
                f"index was built with '{built_by}' but the service is running with "
                f"'{self._embedder_id()}'. Rebuild it: python -m app.cli index"
            )
            return

        try:
            import numpy as np

            self._vectors = np.load(INDEX_VECTORS)["vectors"]
            self._chunks = [Chunk(**c) for c in json.loads(
                INDEX_CHUNKS.read_text(encoding="utf-8"))]
            self._loaded = True
        except Exception as exc:
            self._error = f"index unreadable: {exc}"

    def ready(self) -> tuple[bool, str]:
        self._load()
        if not self._loaded:
            return False, self._error
        docs = len({c.source for c in self._chunks})
        embedder = "lexical (mock)" if self._cfg.mock else self._cfg.embed_model
        return True, f"{len(self._chunks)} chunks from {docs} documents via {embedder}"

    # -- query ---------------------------------------------------------------
    def search(self, question: str) -> tuple[list[Chunk], list[float]]:
        """Return the top-k chunks and their scores. Empty list means not grounded."""
        self._load()
        if not self._loaded:
            return [], []
        import numpy as np

        if self._cfg.mock:
            scored = [(c, coverage(question, c.text)) for c in self._chunks]
            scored = [(c, s) for c, s in scored if s >= self._cfg.mock_coverage_floor]
            scored.sort(key=lambda x: -x[1])
            scored = scored[:self._cfg.retrieve_top_k]
            return [c for c, _ in scored], [round(s, 4) for _, s in scored]

        q = self._encode([question], kind="query")[0]
        scores = self._vectors @ q
        k = min(self._cfg.retrieve_top_k, len(scores))
        order = np.argsort(-scores)[:k]
        picked = [(self._chunks[i], float(scores[i])) for i in order
                  if float(scores[i]) >= self._cfg.grounding_floor]
        if not picked:
            return [], []
        return [c for c, _ in picked], [s for _, s in picked]
