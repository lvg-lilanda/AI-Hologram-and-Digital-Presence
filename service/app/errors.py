"""One error shape for every failure, so Unity branches on a code and never on prose.

Card acceptance criteria this serves:
  T13  "Silence, invalid audio and engine failure return typed errors."
  T14  "Timeout, unavailable model and invalid output are handled."
"""
from __future__ import annotations

from fastapi import HTTPException


class ErrorCode:
    # transcription
    NO_SPEECH = "NO_SPEECH"
    INVALID_AUDIO = "INVALID_AUDIO"
    STT_UNAVAILABLE = "STT_UNAVAILABLE"
    # inference
    MODEL_TIMEOUT = "MODEL_TIMEOUT"
    MODEL_UNAVAILABLE = "MODEL_UNAVAILABLE"
    INVALID_MODEL_OUTPUT = "INVALID_MODEL_OUTPUT"
    # retrieval
    INDEX_EMPTY = "INDEX_EMPTY"
    # speech
    TTS_UNAVAILABLE = "TTS_UNAVAILABLE"
    # generic
    BAD_REQUEST = "BAD_REQUEST"
    NOT_FOUND = "NOT_FOUND"


#: code -> (http status, retryable). Retryable tells Unity whether a second attempt
#: is worth the latency or whether it should drop straight to the canned fallback.
_POLICY: dict[str, tuple[int, bool]] = {
    ErrorCode.NO_SPEECH: (422, False),
    ErrorCode.INVALID_AUDIO: (415, False),
    ErrorCode.STT_UNAVAILABLE: (503, True),
    ErrorCode.MODEL_TIMEOUT: (504, True),
    ErrorCode.MODEL_UNAVAILABLE: (503, True),
    ErrorCode.INVALID_MODEL_OUTPUT: (502, True),
    ErrorCode.INDEX_EMPTY: (503, False),
    ErrorCode.TTS_UNAVAILABLE: (503, True),
    ErrorCode.BAD_REQUEST: (400, False),
    ErrorCode.NOT_FOUND: (404, False),
}


class ServiceError(HTTPException):
    """Raise this, never a bare HTTPException - the envelope stays uniform."""

    def __init__(self, code: str, message: str, **detail):
        status, retryable = _POLICY.get(code, (500, True))
        super().__init__(
            status_code=status,
            detail={
                "error": {
                    "code": code,
                    "message": message,
                    "retryable": retryable,
                    **detail,
                }
            },
        )
