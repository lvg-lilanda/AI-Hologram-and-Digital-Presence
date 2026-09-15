"""Contract tests. These run in mock mode, so they need no Ollama, no Azure key and
no VX PC - which means Dinesh can run them on his own laptop as part of T20's
evidence matrix, and they can gate a PR before anyone books lab time.

    cd service && python -m pytest -q
"""
from __future__ import annotations

import io
import os
import shutil
import struct
import tempfile
import wave
from pathlib import Path

import pytest

os.environ["AIHOLO_MOCK"] = "true"

from fastapi.testclient import TestClient  # noqa: E402

from app.config import CORPUS_DIR, settings  # noqa: E402
from app.rag import Retriever  # noqa: E402


def wav_bytes(seconds: float = 1.0, rate: int = 16_000, channels: int = 1,
              width: int = 2) -> bytes:
    buf = io.BytesIO()
    with wave.open(buf, "wb") as w:
        w.setnchannels(channels)
        w.setsampwidth(width)
        w.setframerate(rate)
        n = int(seconds * rate) * channels
        w.writeframes(struct.pack(f"<{n}h", *([1000] * n)))
    return buf.getvalue()


@pytest.fixture(scope="module")
def client():
    """Index into a throwaway directory, never var/index.

    The suite runs in mock mode, so a shared index directory would leave a
    lexical-mode index behind and break the next real run - which is exactly what
    scripts/mac_local_setup.sh does when it runs the tests between building the
    real index and starting the real service.
    """
    from app import rag

    tmp = Path(tempfile.mkdtemp(prefix="aiholo-test-index-"))
    originals = {name: getattr(rag, name)
                 for name in ("INDEX_DIR", "INDEX_VECTORS", "INDEX_CHUNKS", "INDEX_META")}
    rag.INDEX_DIR = tmp
    rag.INDEX_VECTORS = tmp / "vectors.npz"
    rag.INDEX_CHUNKS = tmp / "chunks.json"
    rag.INDEX_META = tmp / "meta.json"

    Retriever(settings()).build(CORPUS_DIR)
    from app.main import app

    try:
        yield TestClient(app)
    finally:
        for name, value in originals.items():
            setattr(rag, name, value)
        shutil.rmtree(tmp, ignore_errors=True)


# --- T12: scaffold and health -------------------------------------------------

def test_health_reports_every_component(client):
    body = client.get("/health").json()
    assert set(body) >= {"ok", "mock", "stt", "model", "index", "azure", "version"}
    for part in ("stt", "model", "index", "azure"):
        assert "ready" in body[part] and "detail" in body[part]


def test_no_secret_is_ever_served(client):
    assert "azure_key" not in client.get("/health").text.lower()


# --- T13: transcription -------------------------------------------------------

def test_transcribe_returns_text_and_processing_time(client):
    r = client.post("/transcribe",
                    files={"audio": ("a.wav", wav_bytes(1.0), "audio/wav")})
    assert r.status_code == 200
    body = r.json()
    assert body["text"] and body["processingMs"] >= 0 and body["durationMs"] > 0


def test_wrong_sample_rate_is_typed_invalid_audio(client):
    r = client.post("/transcribe",
                    files={"audio": ("a.wav", wav_bytes(1.0, rate=44_100), "audio/wav")})
    assert r.status_code == 415
    assert r.json()["error"]["code"] == "INVALID_AUDIO"


def test_stereo_is_typed_invalid_audio(client):
    r = client.post("/transcribe",
                    files={"audio": ("a.wav", wav_bytes(1.0, channels=2), "audio/wav")})
    assert r.json()["error"]["code"] == "INVALID_AUDIO"


def test_garbage_body_is_typed_invalid_audio(client):
    r = client.post("/transcribe",
                    files={"audio": ("a.wav", b"not a wav at all", "audio/wav")})
    assert r.json()["error"]["code"] == "INVALID_AUDIO"


def test_too_short_is_typed_no_speech(client):
    r = client.post("/transcribe",
                    files={"audio": ("a.wav", wav_bytes(0.05), "audio/wav")})
    assert r.status_code == 422
    assert r.json()["error"]["code"] == "NO_SPEECH"


# --- T14 / T15: answering, grounding, citations -------------------------------

def test_answer_is_grounded_and_cites_its_source(client):
    r = client.post("/answer", json={"text": "What is Telstra muru-D?",
                                     "sessionId": "test-1"})
    assert r.status_code == 200
    body = r.json()
    assert body["grounded"] is True
    assert body["citations"], "a grounded answer must carry at least one citation"
    first = body["citations"][0]
    assert first["title"]
    assert first["section"] or first["page"] is not None


@pytest.mark.parametrize("question", [
    "What were last night's football scores?",
    "Who won the AFL grand final?",
    "How do I reset my Telstra modem password?",
    "What is the capital of Peru?",
])
def test_unsupported_question_says_so_instead_of_inventing(client, question):
    """The behaviour the client demo is judged on: refuse rather than improvise.

    Either kind of refusal is correct. "not_found" is a blank I-don't-know;
    "out_of_scope" is the better one - the corpus explicitly covers the topic as
    something the avatar does not handle, so the reply can say what it IS for.
    What must never happen is outcome == "answered".
    """
    body = client.post("/answer", json={"text": question, "sessionId": "test-2"}).json()
    assert body["outcome"] in ("not_found", "out_of_scope"), \
        f"wrongly claimed to know: {body['text'][:80]}"
    assert body["grounded"] is False
    if body["outcome"] == "not_found":
        assert body["citations"] == []


def test_out_of_scope_reply_cites_the_passage_it_came_from(client):
    """An out-of-scope refusal is still grounded in an approved passage, and showing
    which one is the evidence a reviewer needs to check the avatar is not improvising
    its idea of what it cannot do."""
    body = client.post("/answer",
                       json={"text": "How do I reset my Telstra modem password?",
                             "sessionId": "oos"}).json()
    if body["outcome"] != "out_of_scope":
        pytest.skip("mock retrieval did not reach the out-of-scope passage")
    assert body["citations"], "an out-of-scope reply should say which passage said so"
    assert body["grounded"] is False


def test_answer_produces_audio_and_a_matching_viseme_track(client):
    body = client.post("/answer", json={"text": "What is Telstra muru-D?",
                                        "sessionId": "test-3"}).json()
    assert body["audioUrl"] and body["visemesUrl"]

    audio = client.get(body["audioUrl"])
    assert audio.status_code == 200 and audio.content[:4] == b"RIFF"

    track = client.get(body["visemesUrl"]).json()
    assert track["id"] == body["id"]
    assert track["fps"] == 60 and track["shapeCount"] == 55
    # VisemeTrack.cs indexes values as frame * shapeCount + i - the flattening has
    # to be exact or the mouth reads the wrong blend shape.
    assert len(track["values"]) == track["frameCount"] * track["shapeCount"]


def test_empty_text_is_rejected(client):
    assert client.post("/answer", json={"text": "", "sessionId": "s"}).status_code == 422


def test_session_memory_is_kept_then_cleared(client):
    from app.main import sessions

    client.post("/answer", json={"text": "What is Telstra muru-D?", "sessionId": "mem"})
    assert sessions.history("mem")
    client.post("/session/mem/reset")
    assert sessions.history("mem") == []


def test_unknown_artifact_is_a_typed_not_found(client):
    r = client.get("/audio/does-not-exist.wav")
    assert r.status_code == 404 and r.json()["error"]["code"] == "NOT_FOUND"


def test_path_traversal_is_refused(client):
    assert client.get("/audio/..%2f..%2f.env").status_code in (404, 400)


def test_answer_still_works_when_speech_is_not_set_up_yet(client, monkeypatch):
    """The exact stage-1 failure: no Azure key and no SDK installed.

    Before this was handled, the Azure adapter raised a bare ImportError, which
    escaped as a 500 with a plain-text body - so /answer was unusable until the
    last stage of bring-up, and the log just showed a JSON decode error with no
    hint that the cause was a missing optional dependency.
    """
    from app import main
    from app.config import Settings
    from app.tts import AzureSpeech

    monkeypatch.setattr(main, "tts", AzureSpeech(Settings(mock=False, azure_key="")))

    r = client.post("/answer", json={"text": "What is Telstra muru-D?",
                                     "sessionId": "no-speech"})
    assert r.status_code == 200, r.text
    body = r.json()
    assert body["text"]
    assert body["audioUrl"] is None and body["visemesUrl"] is None
    assert body["speechUnavailable"]


def test_configured_but_broken_speech_is_still_a_typed_error(client, monkeypatch):
    """Not set up yet and actually broken must not look the same."""
    from app import main
    from app.errors import ErrorCode, ServiceError

    class BrokenSpeech:
        @staticmethod
        def ready():
            return True, "configured"

        @staticmethod
        def synthesise(answer_id, text):
            raise ServiceError(ErrorCode.TTS_UNAVAILABLE, "Azure returned 401")

    monkeypatch.setattr(main, "tts", BrokenSpeech())
    r = client.post("/answer", json={"text": "What is Telstra muru-D?",
                                     "sessionId": "broken-speech"})
    assert r.status_code == 503
    assert r.json()["error"]["code"] == "TTS_UNAVAILABLE"


class _FakeLlm:
    """Returns a scripted (status, answer, usedPassages) so the classifier can be
    tested without depending on what a real model happens to say today."""

    def __init__(self, status, answer="Some reply.", used=(1,)):
        self._status, self._answer, self._used = status, answer, list(used)

    def ready(self):
        return True, "fake"

    def generate(self, question, passages, history):
        return self._status, self._answer, ["a", "b"], self._used, 1


def test_a_refusal_written_in_prose_is_still_a_refusal(client, monkeypatch):
    """The bug this replaced: llama3.1 refuses by paraphrasing rather than emitting
    a sentinel string, so matching on the answer TEXT scored correct refusals as
    inventions. The status field decides now; the wording is irrelevant."""
    from app import main

    monkeypatch.setattr(main, "llm", _FakeLlm(
        "not_found", "I don't have anything on that in the material I've been given."))
    body = client.post("/answer", json={"text": "Who won the AFL grand final?",
                                        "sessionId": "prose"}).json()
    assert body["outcome"] == "not_found"
    assert body["grounded"] is False


def test_model_cannot_invent_a_scope_boundary(client, monkeypatch):
    """Guard 1. If the model claims out_of_scope but no passage marked out-of-scope
    was retrieved, it is making the boundary up - the corpus owns that, not the
    model. Downgraded to not_found rather than trusted."""
    from app import main

    monkeypatch.setattr(main, "llm", _FakeLlm("out_of_scope", "I can't help with that."))
    body = client.post("/answer", json={"text": "What is Telstra muru-D?",
                                        "sessionId": "invented-scope"}).json()
    # "What is Telstra muru-D?" retrieves in-scope passages, so the claim is bogus.
    assert body["outcome"] == "not_found"


def test_unrecognised_status_never_becomes_an_answer(client, monkeypatch):
    """A garbled status must fail toward refusing. Failing the other way means the
    avatar asserts something unverified in front of the client."""
    from app import main

    monkeypatch.setattr(main, "llm", _FakeLlm("probably?", "Telstra is a company."))
    body = client.post("/answer", json={"text": "What is Telstra muru-D?",
                                        "sessionId": "garbled"}).json()
    assert body["grounded"] is False


def test_not_found_wording_is_fixed_not_model_written(client, monkeypatch):
    """A refusal that varies run to run can leak a half-remembered fact in its
    phrasing. One fixed sentence instead."""
    from app import main

    monkeypatch.setattr(main, "llm", _FakeLlm(
        "not_found", "I think the Wallabies won but I'm not certain."))
    body = client.post("/answer", json={"text": "Who won?", "sessionId": "leak"}).json()
    assert "Wallabies" not in body["text"]
    assert body["text"] == main.UNGROUNDED_REPLY


def test_session_history_is_readable_over_http(client):
    """Multi-turn memory is otherwise invisible - you can only infer it from whether
    a follow-up answer looks right. Reading it back is what makes a memory bug
    diagnosable in the lab rather than mysterious."""
    client.post("/answer", json={"text": "What is Telstra muru-D?", "sessionId": "hist"})
    turns = client.get("/session/hist").json()["turns"]
    assert [t["role"] for t in turns] == ["user", "assistant"]
    assert turns[0]["text"] == "What is Telstra muru-D?"

    client.post("/session/hist/reset")
    assert client.get("/session/hist").json()["turns"] == []


def test_unknown_session_reads_as_empty_not_an_error(client):
    """A session the service has never seen is a normal state - the first question
    of every conversation. It must not look like a failure."""
    r = client.get("/session/never-seen-before")
    assert r.status_code == 200
    assert r.json()["turns"] == []


def test_an_answer_that_cites_nothing_is_not_an_answer(client, monkeypatch):
    """The measured failure: asked a vague follow-up, llama3.1 said muru-D "focuses
    on supporting startups and entrepreneurs" - true of the real accelerator, absent
    from this corpus, stated with full confidence. Weak retrieval left a gap and the
    model filled it from training. Requiring it to name a passage closes that."""
    from app import main

    monkeypatch.setattr(main, "llm", _FakeLlm(
        "answered",
        "muru-D focuses on supporting startups and entrepreneurs.",
        used=[]))
    body = client.post("/answer", json={"text": "tell me about the project",
                                        "sessionId": "unsourced"}).json()
    assert body["outcome"] == "not_found"
    assert "startups" not in body["text"]


def test_citing_a_passage_that_was_never_retrieved_does_not_count(client, monkeypatch):
    """Naming passage 97 is not evidence. Out-of-range indices are dropped, and an
    answer left with none of them fails the same way as citing nothing."""
    from app import main

    monkeypatch.setattr(main, "llm", _FakeLlm("answered", "Confident nonsense.",
                                              used=[97, -3, 0]))
    body = client.post("/answer", json={"text": "What is Telstra muru-D?",
                                        "sessionId": "bad-cite"}).json()
    assert body["outcome"] == "not_found"


def test_citations_list_only_what_the_answer_used(client, monkeypatch):
    """Citing all four retrieved passages when the answer came from one reads like
    corroboration that is not there - and it is what a reviewer checks first."""
    from app import main

    monkeypatch.setattr(main, "llm", _FakeLlm("answered", "From passage two.", used=[2]))
    body = client.post("/answer", json={"text": "What is Telstra muru-D?",
                                        "sessionId": "one-cite"}).json()
    assert body["outcome"] == "answered"
    assert len(body["citations"]) == 1


def test_a_vague_follow_up_retrieves_using_the_previous_question(client, monkeypatch):
    """"ok tell me about the project" carries almost no signal alone. Retrieval has
    to see what was being discussed, or it returns near-random passages and hands the
    model a gap to invent into."""
    from app import main

    seen = []
    real_search = main.retriever.search
    monkeypatch.setattr(main.retriever, "search",
                        lambda q: (seen.append(q), real_search(q))[1])

    client.post("/answer", json={"text": "What is Telstra muru-D?", "sessionId": "ctx"})
    client.post("/answer", json={"text": "tell me about it", "sessionId": "ctx"})

    assert seen[0] == "What is Telstra muru-D?"
    assert "Telstra muru-D" in seen[1] and "tell me about it" in seen[1]
