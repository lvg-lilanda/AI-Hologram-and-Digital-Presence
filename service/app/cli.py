"""One documented command per maintenance job. T15: "Approved documents can be
indexed with one documented command."

    python -m app.index          # build the retrieval index from service/corpus
    python -m app.cli index      # same thing
    python -m app.cli check      # print readiness of every component
"""
from __future__ import annotations

import sys

from .config import CORPUS_DIR, settings


def cmd_index() -> int:
    from .rag import EmbeddingUnavailable, Retriever

    cfg = settings()
    try:
        n = Retriever(cfg).build(CORPUS_DIR)
    except EmbeddingUnavailable as exc:
        print(f"FAILED: {exc}\n\nStart Ollama and pull the embedding model:\n"
              f"    ollama serve &\n    ollama pull {cfg.embed_model}", file=sys.stderr)
        return 1
    except ValueError as exc:
        print(f"FAILED: {exc}", file=sys.stderr)
        return 1
    print(f"Indexed {n} chunks from {CORPUS_DIR}")
    return 0


def cmd_check() -> int:
    from .llm import build_llm
    from .rag import Retriever
    from .stt import build_stt
    from .tts import build_tts

    cfg = settings()
    rows = [
        ("stt", build_stt(cfg).ready()),
        ("model", build_llm(cfg).ready()),
        ("index", Retriever(cfg).ready()),
        ("azure", build_tts(cfg).ready()),
    ]
    worst = 0
    for name, (ok, detail) in rows:
        # "later stage, not reached yet" is not the same as "broken", and printing
        # both as DOWN makes the real one invisible.
        pending = detail.startswith(("NOT_INSTALLED", "NOT_CONFIGURED"))
        state = "OK  " if ok else ("--  " if pending else "DOWN")
        print(f"{state}  {name:6} {detail}")
        if not ok and not pending:
            worst = 1
    return worst


def main() -> int:
    cmd = sys.argv[1] if len(sys.argv) > 1 else "check"
    return {"index": cmd_index, "check": cmd_check}.get(cmd, cmd_check)()


if __name__ == "__main__":
    raise SystemExit(main())
