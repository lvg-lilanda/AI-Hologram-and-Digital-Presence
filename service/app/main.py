"""The localhost AI service. Unity is the only client.

Routes (the full contract is in docs/AI_SERVICE_CONTRACT.md):
    GET  /health              readiness of stt, model, index and Azure
    POST /transcribe          16 kHz mono 16-bit WAV  -> text + processing time
    POST /answer              text + sessionId        -> reply, citations, audio, visemes
    GET  /audio/{file}        the generated wav
    GET  /visemes/{file}      the generated viseme track
    GET  /session/{id}        what the avatar currently remembers
    POST /session/{id}/reset  forget one conversation

Binding: 127.0.0.1 by default (T12). The VX PC runs Unity and this service on the
same machine, so nothing needs to leave the loopback interface, and not listening
on the network is the whole of the security story for a lab PC nobody administers.
"""
from __future__ import annotations

import logging
import time

from fastapi import FastAPI, Request, UploadFile
from fastapi.responses import FileResponse, JSONResponse

from .config import AUDIO_DIR, VISEME_DIR, settings
from .contracts import (
    AnswerRequest,
    AnswerResponse,
    AnswerTimings,
    Citation,
    ComponentHealth,
    HealthResponse,
    SessionResponse,
    SessionTurn,
    TranscribeResponse,
)
from .errors import ErrorCode, ServiceError
from .llm import build_llm
from .rag import EmbeddingUnavailable, Retriever
from .sessions import SessionStore
from .stt import build_stt
from .store import new_id, save
from .tts import build_tts

VERSION = "0.1.0-sprint2"

log = logging.getLogger("aiholo")

UNGROUNDED_REPLY = (
    "I don't have anything on that in the material I've been given. "
    "Try asking me about the project, the display, or what I'm for."
)

app = FastAPI(title="Team 11 AI Hologram service", version=VERSION)

cfg = settings()
stt = build_stt(cfg)
llm = build_llm(cfg)
tts = build_tts(cfg)
retriever = Retriever(cfg)
sessions = SessionStore()


@app.exception_handler(ServiceError)
async def _service_error(request: Request, exc: ServiceError):
    # Uvicorn's access log records only the status code, so a 503 in service.log used
    # to say nothing about WHICH component failed - which made a crash-plus-503 pair
    # impossible to diagnose after the fact. Log the code and message here.
    error = exc.detail.get("error", {})
    log.warning("%s %s -> %s: %s", request.method, request.url.path,
                error.get("code"), error.get("message"))
    return JSONResponse(status_code=exc.status_code, content=exc.detail)


@app.exception_handler(Exception)
async def _unhandled(request: Request, exc: Exception):
    # An unhandled exception returns a plain-text 500 by default, which is how the
    # missing Azure SDK first showed up as an unreadable JSON error at the client.
    log.exception("unhandled error on %s %s", request.method, request.url.path)
    return JSONResponse(
        status_code=500,
        content={"error": {"code": "INTERNAL", "message": str(exc)[:300],
                           "retryable": False}},
    )


def _component(pair: tuple[bool, str]) -> ComponentHealth:
    ok, detail = pair
    return ComponentHealth(ready=ok, detail=detail)


@app.get("/health", response_model=HealthResponse)
def health() -> HealthResponse:
    """Every component reports separately, so a red line names the thing to fix
    instead of just saying the service is down."""
    parts = {
        "stt": _component(stt.ready()),
        "model": _component(llm.ready()),
        "index": _component(retriever.ready()),
        "azure": _component(tts.ready()),
    }
    return HealthResponse(
        ok=all(p.ready for p in parts.values()), mock=cfg.mock, version=VERSION, **parts
    )


@app.post("/transcribe", response_model=TranscribeResponse)
async def transcribe(audio: UploadFile) -> TranscribeResponse:
    if audio.content_type not in (None, "audio/wav", "audio/x-wav",
                                  "application/octet-stream"):
        raise ServiceError(
            ErrorCode.INVALID_AUDIO,
            f"Unsupported content type '{audio.content_type}'.",
            expected="audio/wav",
        )
    result = stt.transcribe(await audio.read())
    return TranscribeResponse(
        text=result.text,
        durationMs=result.duration_ms,
        processingMs=result.processing_ms,
        engine=result.engine,
        language=result.language,
    )


@app.post("/answer", response_model=AnswerResponse)
def answer(req: AnswerRequest) -> AnswerResponse:
    started = time.perf_counter()
    answer_id = new_id()

    ready, detail = retriever.ready()
    if not ready:
        raise ServiceError(ErrorCode.INDEX_EMPTY, detail)

    # Retrieval query, not the raw question. A follow-up like "ok tell me about the
    # project" carries almost no signal on its own, so it embeds poorly and retrieves
    # the wrong passages - which is what left the model with a gap to fill from its
    # own knowledge. Prepending the previous question restores the subject at no
    # extra latency; the model still receives the user's real words.
    history = sessions.history(req.sessionId)
    previous = next((t.text for t in reversed(history) if t.role == "user"), "")
    search_text = f"{previous} {req.text}".strip() if previous else req.text

    t0 = time.perf_counter()
    try:
        passages, scores = retriever.search(search_text)
    except EmbeddingUnavailable as exc:
        # Retrieval and inference share one runtime, so Ollama being down takes
        # both out. Report it as the model being unavailable - that is the thing
        # the operator has to restart.
        raise ServiceError(ErrorCode.MODEL_UNAVAILABLE, str(exc)) from exc
    retrieve_ms = int((time.perf_counter() - t0) * 1000)

    status, text, follow_ups, used, infer_ms = llm.generate(req.text, passages, history)

    # Which passages the answer actually came from. Citing everything retrieved
    # overstates the evidence - four citations on an answer drawn from one of them
    # reads like corroboration that is not there.
    used_pairs = [(passages[i - 1], scores[i - 1]) for i in used
                  if 1 <= i <= len(passages)]

    # Three outcomes, not two. "That is not something I handle, here is what I can
    # do" is a REFUSAL, but a useful one - it comes from an approved passage and
    # reads far better than a blank "I don't know". Collapsing it into `answered`
    # would make the grounding evaluation count good refusals as inventions;
    # collapsing it into `not_found` would throw away the helpful reply.
    #
    # The model classifies its own reply, with three guards over the top:
    retrieved_out_of_scope = any(c.out_of_scope for c in passages)

    if not passages:
        outcome = "not_found"
    elif status == "answered" and not used_pairs:
        # Guard 1: an answer that cannot name a real retrieved passage is not
        # grounded in anything. Measured: asked a vague follow-up, llama3.1 answered
        # about muru-D "supporting startups and entrepreneurs" - true of the real
        # accelerator, absent from this corpus, and delivered with full confidence.
        # A prompt saying "use only the passages" did not stop it; having to point
        # at one does.
        outcome = "not_found"
    elif status == "out_of_scope" and not retrieved_out_of_scope:
        # Guard 2: it cannot declare something out of scope unless the CORPUS says
        # so. The scope boundary belongs to Anuji's documents, not to whatever the
        # model feels like refusing today.
        outcome = "not_found"
    else:
        outcome = status

    if outcome == "not_found":
        # Guard 3: one fixed sentence, so a refusal never varies between runs and
        # never leaks a half-remembered fact in the phrasing.
        text, follow_ups = UNGROUNDED_REPLY, []
    elif outcome == "out_of_scope":
        follow_ups = []

    grounded = outcome == "answered"

    # Speech that is CONFIGURED but failing is a real error and surfaces as
    # TTS_UNAVAILABLE. Speech that is simply not set up yet - stage 1 and 2 of
    # bring-up, no Azure key, SDK not installed - is not: the text answer is still
    # the useful part, and failing the whole request over it would make /answer
    # untestable until the last stage. The difference is visible in the response
    # rather than guessed at.
    audio_url = viseme_url = None
    speech_unavailable = None
    tts_ms = 0
    if req.speak:
        speech_ready, speech_detail = tts.ready()
        if speech_ready:
            wav, track, tts_ms = tts.synthesise(answer_id, text)
            audio_url, viseme_url = save(answer_id, wav, track)
        else:
            speech_unavailable = speech_detail

    sessions.record(req.sessionId, req.text, text)

    return AnswerResponse(
        id=answer_id,
        sessionId=req.sessionId,
        text=text,
        followUps=follow_ups,
        # Citations stand for an out-of-scope reply too: it is grounded in an
        # approved passage, and showing which one is exactly the evidence a
        # reviewer wants. Only "not_found" has nothing to cite.
        citations=[
            Citation(title=c.title, section=c.section, page=c.page, score=round(sc, 4))
            for c, sc in used_pairs
        ] if outcome != "not_found" else [],
        outcome=outcome,
        grounded=grounded,
        isFallback=False,
        audioUrl=audio_url,
        visemesUrl=viseme_url,
        speechUnavailable=speech_unavailable,
        timings=AnswerTimings(
            retrieveMs=retrieve_ms,
            inferMs=infer_ms,
            ttsMs=tts_ms,
            totalMs=int((time.perf_counter() - started) * 1000),
        ),
    )


@app.get("/audio/{name}")
def audio_file(name: str) -> FileResponse:
    path = (AUDIO_DIR / name).resolve()
    if path.parent != AUDIO_DIR.resolve() or not path.is_file():
        raise ServiceError(ErrorCode.NOT_FOUND, f"No generated audio named '{name}'.")
    return FileResponse(path, media_type="audio/wav")


@app.get("/visemes/{name}")
def viseme_file(name: str) -> FileResponse:
    path = (VISEME_DIR / name).resolve()
    if path.parent != VISEME_DIR.resolve() or not path.is_file():
        raise ServiceError(ErrorCode.NOT_FOUND, f"No viseme track named '{name}'.")
    return FileResponse(path, media_type="application/json")


@app.get("/session/{session_id}", response_model=SessionResponse)
def read_session(session_id: str) -> SessionResponse:
    return SessionResponse(
        sessionId=session_id,
        turns=[SessionTurn(role=t.role, text=t.text)
               for t in sessions.history(session_id)],
    )


@app.post("/session/{session_id}/reset")
def reset_session(session_id: str) -> dict:
    sessions.reset(session_id)
    return {"sessionId": session_id, "cleared": True}


def run() -> None:
    import uvicorn

    uvicorn.run(app, host=cfg.host, port=cfg.port, log_level="info")


if __name__ == "__main__":
    run()
