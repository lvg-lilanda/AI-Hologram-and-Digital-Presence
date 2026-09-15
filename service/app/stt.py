"""Speech to text, behind an adapter boundary.

T07's deliverable is "a selected local STT engine/configuration behind an adapter
boundary" - so the engine choice lives in one class and the rest of the service only
ever sees SttAdapter. Swapping faster-whisper for whisper.cpp or Vosk after the
benchmark means adding one class here and changing one line in _build().

Agreed microphone format (T13, and what Unity sends):
    RIFF WAV, 16 kHz, mono, 16-bit signed PCM.
Unity's Microphone.Start gives a float AudioClip; Lilan's capture script converts to
this before POSTing. Anything else is rejected as INVALID_AUDIO rather than silently
resampled, because a silent resample is how a demo ends up with a 3-second lag nobody
can explain.
"""
from __future__ import annotations

import io
import os
import time
import wave
from dataclasses import dataclass

# huggingface_hub's xet transfer backend (hf_xet) opens dozens of CAS connections
# and, on this network, never progresses past 0 bytes on the whisper model weights -
# the model load hangs indefinitely instead of failing. The plain HTTP downloader
# does not have this problem. Must be set before faster_whisper (and transitively
# huggingface_hub) is imported anywhere in the process.
os.environ.setdefault("HF_HUB_DISABLE_XET", "1")
from typing import Protocol

from .config import Settings
from .errors import ErrorCode, ServiceError

EXPECTED_RATE = 16_000
EXPECTED_CHANNELS = 1
EXPECTED_SAMPWIDTH = 2  # bytes, 16-bit
MIN_SPEECH_SECONDS = 0.25


@dataclass
class Transcript:
    text: str
    duration_ms: int
    processing_ms: int
    engine: str
    language: str = "en"


class SttAdapter(Protocol):
    name: str

    def ready(self) -> tuple[bool, str]: ...

    def transcribe(self, wav_bytes: bytes) -> Transcript: ...


def validate_wav(wav_bytes: bytes) -> tuple[int, float]:
    """Return (frame count, seconds) or raise INVALID_AUDIO.

    Checked before the engine is touched so a malformed upload costs milliseconds
    rather than a model load.
    """
    if not wav_bytes:
        raise ServiceError(ErrorCode.INVALID_AUDIO, "Empty request body.")
    try:
        with wave.open(io.BytesIO(wav_bytes), "rb") as w:
            rate, channels, width, frames = (
                w.getframerate(), w.getnchannels(), w.getsampwidth(), w.getnframes()
            )
    except (wave.Error, EOFError) as exc:
        raise ServiceError(
            ErrorCode.INVALID_AUDIO,
            f"Not a readable RIFF WAV file: {exc}",
            expected="16 kHz mono 16-bit PCM WAV",
        ) from exc

    if (rate, channels, width) != (EXPECTED_RATE, EXPECTED_CHANNELS, EXPECTED_SAMPWIDTH):
        raise ServiceError(
            ErrorCode.INVALID_AUDIO,
            "Audio format does not match the agreed microphone contract.",
            expected="16000 Hz, 1 channel, 16-bit PCM",
            received=f"{rate} Hz, {channels} channel(s), {width * 8}-bit",
        )
    if frames == 0:
        raise ServiceError(ErrorCode.NO_SPEECH, "The recording contains no audio frames.")
    return frames, frames / float(rate)


def check_speech_floor(seconds: float) -> None:
    """Applied by every adapter, mock included - a Unity path tested against a mock
    that accepts a 50 ms blip would fail the first time the lab uses the real engine."""
    if seconds < MIN_SPEECH_SECONDS:
        raise ServiceError(
            ErrorCode.NO_SPEECH,
            f"Recording is {seconds:.2f}s, shorter than the {MIN_SPEECH_SECONDS}s floor.",
        )


class FasterWhisperAdapter:
    """Default local engine. Model size and compute type come from the benchmark."""

    name = "faster-whisper"

    def __init__(self, cfg: Settings):
        self._cfg = cfg
        self._model = None
        self._load_error = ""

    def _load(self):
        if self._model is not None or self._load_error:
            return
        try:
            from faster_whisper import WhisperModel

            device = self._cfg.stt_device
            if device == "auto":
                device = "auto"  # faster-whisper resolves cuda/cpu itself
            self._model = WhisperModel(
                self._cfg.stt_model, device=device, compute_type=self._cfg.stt_compute
            )
        except ImportError as exc:
            self._load_error = f"NOT_INSTALLED: faster-whisper is stage 2 ({exc})"
        except Exception as exc:  # missing model, no disk space, bad compute type
            self._load_error = str(exc)

    def ready(self) -> tuple[bool, str]:
        self._load()
        if self._model is None:
            return False, self._load_error or "not loaded"
        return True, f"{self._cfg.stt_model} ({self._cfg.stt_compute})"

    def transcribe(self, wav_bytes: bytes) -> Transcript:
        _, seconds = validate_wav(wav_bytes)
        check_speech_floor(seconds)

        self._load()
        if self._model is None:
            raise ServiceError(
                ErrorCode.STT_UNAVAILABLE, f"Speech engine failed to load: {self._load_error}"
            )

        started = time.perf_counter()
        try:
            segments, info = self._model.transcribe(
                io.BytesIO(wav_bytes), language="en", vad_filter=True, beam_size=1
            )
            text = " ".join(s.text.strip() for s in segments).strip()
        except Exception as exc:
            raise ServiceError(
                ErrorCode.STT_UNAVAILABLE, f"Transcription failed: {exc}"
            ) from exc
        processing_ms = int((time.perf_counter() - started) * 1000)

        if not text:
            # VAD removed everything - a real outcome, not an error in the engine.
            raise ServiceError(
                ErrorCode.NO_SPEECH,
                "No speech detected in the recording.",
                processingMs=processing_ms,
            )

        return Transcript(
            text=text,
            duration_ms=int(seconds * 1000),
            processing_ms=processing_ms,
            engine=f"{self.name}:{self._cfg.stt_model}",
            language=getattr(info, "language", "en") or "en",
        )


class MockSttAdapter:
    """Lets Unity be built and tested before the VX PC or a model download exists."""

    name = "mock"

    def ready(self) -> tuple[bool, str]:
        return True, "mock adapter - not a real transcription"

    def transcribe(self, wav_bytes: bytes) -> Transcript:
        _, seconds = validate_wav(wav_bytes)
        check_speech_floor(seconds)
        return Transcript(
            text="What is Telstra muru-D?",
            duration_ms=int(seconds * 1000),
            processing_ms=5,
            engine="mock",
        )


def build_stt(cfg: Settings) -> SttAdapter:
    return MockSttAdapter() if cfg.mock else FasterWhisperAdapter(cfg)
