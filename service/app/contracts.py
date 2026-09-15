"""The API contract. These models ARE the agreement with Unity - if a field moves,
Lilan's LiveAnswerSource breaks, so a change here needs a note in
docs/AI_SERVICE_CONTRACT.md and a word to Lilan before it is merged.

Field names are camelCase on purpose: Unity's JsonUtility maps JSON keys to C# field
names literally and cannot rename, so the wire format has to match the C# side.
"""
from __future__ import annotations

from pydantic import BaseModel, Field


class ComponentHealth(BaseModel):
    ready: bool
    detail: str = ""


class HealthResponse(BaseModel):
    """GET /health - T12: 'reports STT, model, index and Azure readiness'."""

    ok: bool
    mock: bool
    stt: ComponentHealth
    model: ComponentHealth
    index: ComponentHealth
    azure: ComponentHealth
    version: str


class TranscribeResponse(BaseModel):
    """POST /transcribe - T13: 'returns text and measured processing time'."""

    text: str
    durationMs: int = Field(description="Length of the submitted audio.")
    processingMs: int = Field(description="Wall-clock time spent transcribing.")
    engine: str
    language: str = "en"


class Citation(BaseModel):
    """T15: 'Retrieved passages include source title and section or page.'"""

    title: str
    section: str = ""
    page: int | None = None
    score: float


class AnswerTimings(BaseModel):
    retrieveMs: int = 0
    inferMs: int = 0
    ttsMs: int = 0
    totalMs: int = 0


class AnswerRequest(BaseModel):
    """T14: 'POST /answer accepts recognised text and a session identifier.'"""

    text: str = Field(min_length=1, max_length=1000)
    sessionId: str = Field(min_length=1, max_length=128)
    speak: bool = True


class AnswerResponse(BaseModel):
    """What the avatar needs to speak one reply.

    Maps 1:1 onto Team11.AI.Answer:
        id         -> Answer.Id
        text       -> Answer.Text
        audioUrl   -> fetched into Answer.Audio via UnityWebRequestMultimedia
        visemesUrl -> fetched into Answer.VisemeJson as raw text
        followUps  -> Answer.FollowUps
        isFallback -> Answer.IsFallback

    The viseme track is a URL rather than an inline object because one 16-second reply
    is ~53,000 floats; inlining it would put a megabyte of JSON through JsonUtility on
    the main thread. This also mirrors the Sprint 1 layout Unity already reads
    (Resources/clips/<id>.wav + Resources/visemes/<id>.json), so VisemePlayer needs
    no new parsing path.
    """

    id: str
    sessionId: str
    text: str
    followUps: list[str] = []
    citations: list[Citation] = []
    #: "answered"     - the corpus covers the question
    #: "out_of_scope" - the corpus explicitly says this is not handled; the reply is
    #:                  a helpful refusal ("that's Telstra support, not me")
    #: "not_found"    - nothing relevant was retrieved at all
    outcome: str
    #: True only for "answered". Kept because Unity branches on it.
    grounded: bool
    isFallback: bool = False
    audioUrl: str | None = None
    visemesUrl: str | None = None
    #: Set when the answer has no audio because speech is not configured yet
    #: (stage 1/2 of bring-up). Null once Azure is wired up. Unity should treat a
    #: null audioUrl as a fallback case regardless of why.
    speechUnavailable: str | None = None
    timings: AnswerTimings = AnswerTimings()


class SessionTurn(BaseModel):
    role: str
    text: str


class SessionResponse(BaseModel):
    """GET /session/{id} - what the model will actually be shown as history.

    Multi-turn memory is otherwise invisible: you can only infer it from whether a
    follow-up answer looks right. Being able to read it back is what makes a memory
    bug diagnosable in the lab instead of mysterious.
    """

    sessionId: str
    turns: list[SessionTurn] = []


class VisemeTrackModel(BaseModel):
    """Mirrors Team11.AI.VisemeTrack exactly - see Assets/Team 11/AI_Questions/VisemeTrack.cs.

    `values` is flattened (frame * shapeCount + i) because Unity's JsonUtility cannot
    parse float[][]. Do not nest it.
    """

    id: str
    fps: int = 60
    shapeCount: int = 55
    frameCount: int = 0
    values: list[float] = []
    visemeIds: list[int] = []
    visemeOffsetsMs: list[float] = []
