"""What happens when Ollama is not running.

This is the failure the team will actually hit - somebody opens Unity before
starting the service, or the VX PC reboots mid-demo. The operator needs to be told
which thing to start, so these assert on the typed code and on the message naming
Ollama, not just on "it raised something".
"""
from __future__ import annotations

import os
from pathlib import Path

import pytest

# These tests build their Settings explicitly, so they neither need nor should touch
# AIHOLO_MOCK. Mutating os.environ at import time leaks across the whole pytest
# session and breaks whichever module imports next - which is exactly what it did.
from app.config import Settings  # noqa: E402
from app.rag import EmbeddingUnavailable, Retriever  # noqa: E402


@pytest.fixture
def offline_cfg(tmp_path):
    # A port nothing is listening on - a stand-in for Ollama being down.
    return Settings(mock=False, ollama_url="http://127.0.0.1:1", embed_timeout_s=2.0)


def test_embedding_failure_is_typed_and_names_ollama(offline_cfg):
    with pytest.raises(EmbeddingUnavailable) as exc:
        Retriever(offline_cfg)._encode(["anything"])
    assert "ollama" in str(exc.value).lower()
    assert offline_cfg.embed_model in str(exc.value)


def test_index_build_fails_loudly_rather_than_writing_a_broken_index(offline_cfg):
    from app.config import CORPUS_DIR

    with pytest.raises(EmbeddingUnavailable):
        Retriever(offline_cfg).build(CORPUS_DIR)


def test_query_and_document_embeddings_use_different_prefixes(monkeypatch, offline_cfg):
    """nomic-embed-text is asymmetric. Sending the question with the document
    prefix silently costs retrieval quality, which is the kind of bug that only
    shows up as 'the avatar is a bit dumb'."""
    seen = []

    class FakeResponse:
        @staticmethod
        def raise_for_status():
            pass

        @staticmethod
        def json():
            return {"embeddings": [[1.0, 0.0]]}

    def fake_post(url, json, timeout):
        seen.append(json["input"][0])
        return FakeResponse()

    monkeypatch.setattr("app.rag.httpx.post", fake_post)
    r = Retriever(offline_cfg)
    r._encode(["a passage"], kind="document")
    r._encode(["a question"], kind="query")

    assert seen[0].startswith("search_document: ")
    assert seen[1].startswith("search_query: ")


def test_index_built_by_another_embedder_is_refused(offline_cfg, tmp_path, monkeypatch):
    """A mock-built index loading silently into a real run would give nonsense
    retrieval with no error anywhere - the worst failure this service has."""
    from app import rag

    monkeypatch.setattr(rag, "INDEX_DIR", tmp_path)
    monkeypatch.setattr(rag, "INDEX_VECTORS", tmp_path / "vectors.npz")
    monkeypatch.setattr(rag, "INDEX_CHUNKS", tmp_path / "chunks.json")
    monkeypatch.setattr(rag, "INDEX_META", tmp_path / "meta.json")

    import json

    import numpy as np

    np.savez_compressed(tmp_path / "vectors.npz", vectors=np.zeros((1, 1), "float32"))
    (tmp_path / "chunks.json").write_text(json.dumps(
        [{"text": "x", "title": "t", "section": "", "page": None, "source": "s"}]))
    (tmp_path / "meta.json").write_text(json.dumps(
        {"schema": rag.INDEX_SCHEMA, "embedder": "lexical(mock)"}))

    ready, detail = rag.Retriever(offline_cfg).ready()
    assert ready is False
    assert "lexical(mock)" in detail and "app.cli index" in detail


def test_index_from_an_older_schema_is_refused(offline_cfg, tmp_path, monkeypatch):
    """An index built before out-of-scope marking existed has no out_of_scope flags,
    so every refusal passage would silently read as an answer. Stale index, wrong
    behaviour, no error - refuse it instead."""
    from app import rag

    monkeypatch.setattr(rag, "INDEX_VECTORS", tmp_path / "vectors.npz")
    monkeypatch.setattr(rag, "INDEX_CHUNKS", tmp_path / "chunks.json")
    monkeypatch.setattr(rag, "INDEX_META", tmp_path / "meta.json")

    import json

    import numpy as np

    np.savez_compressed(tmp_path / "vectors.npz", vectors=np.zeros((1, 1), "float32"))
    (tmp_path / "chunks.json").write_text(json.dumps(
        [{"text": "x", "title": "t", "section": "", "page": None, "source": "s"}]))
    (tmp_path / "meta.json").write_text(json.dumps(
        {"schema": 1, "embedder": offline_cfg.embed_model}))

    ready, detail = rag.Retriever(offline_cfg).ready()
    assert ready is False
    assert "schema" in detail and "app.cli index" in detail


def test_var_dir_is_overridable_so_experiments_cannot_clobber_a_live_index(tmp_path):
    """This exists because it happened: a mock-mode reindex run for testing
    overwrote the real index the service was serving, and the failure only showed
    up later at the other end as INDEX_EMPTY. Generated state has to be divertible.
    """
    import subprocess
    import sys

    env = {**os.environ, "AIHOLO_VAR_DIR": str(tmp_path / "elsewhere")}
    out = subprocess.run(
        [sys.executable, "-c",
         "from app.config import VAR_DIR, INDEX_DIR, AUDIO_DIR; "
         "print(VAR_DIR); print(INDEX_DIR); print(AUDIO_DIR)"],
        capture_output=True, text=True, env=env, cwd=str(Path(__file__).resolve().parent.parent),
    )
    assert out.returncode == 0, out.stderr
    var_dir, index_dir, audio_dir = out.stdout.strip().splitlines()
    assert var_dir == str(tmp_path / "elsewhere")
    # Everything generated hangs off it, not just the index.
    assert index_dir.startswith(var_dir) and audio_dir.startswith(var_dir)
