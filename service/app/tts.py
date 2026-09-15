"""Azure Speech: one request produces the answer audio AND its matching facial timeline.

T25 acceptance criteria:
  - "One request produces answer audio and its matching facial timeline."
        synthesise() returns (wav bytes, VisemeTrackModel) from a single synthesis.
  - "Azure credentials remain outside Unity and source control."
        the key is read from service/.env here; Unity never sees it.
  - "Audio format and facial-frame timing match the Unity contract."
        Riff16Khz16BitMonoPcm, and a 55-value 60 fps track in exactly the flattened
        layout Assets/Team 11/AI_Questions/VisemeTrack.cs parses.

Why blendshapes and not viseme ids alone: VisemeShapeMapBuilder already resolves
Azure's documented 55 positions against Jake's morph targets by name, so the frames
land straight on the existing rig. visemeIds/visemeOffsetsMs are carried too, because
uLipSync's fallback path and any coarse mouth-shape debugging both want them.
"""
from __future__ import annotations

import json
import math
import os
import struct
import subprocess
import sys
import time
import wave
from io import BytesIO

from .config import AUDIO_DIR, SERVICE_ROOT, VAR_DIR, Settings
from .contracts import VisemeTrackModel
from .errors import ErrorCode, ServiceError

SHAPE_COUNT = 55
FPS = 60
SAMPLE_RATE = 16_000
JAW_OPEN_INDEX = 17  # Azure position 18, 1-based - see VisemeShapeMapBuilder


def _escape(text: str) -> str:
    return (text.replace("&", "&amp;").replace("<", "&lt;").replace(">", "&gt;")
                .replace('"', "&quot;"))


def _ssml(text: str, voice: str, locale: str = "en-AU") -> str:
    return (
        f'<speak version="1.0" xmlns="http://www.w3.org/2001/10/synthesis" '
        f'xmlns:mstts="http://www.w3.org/2001/mstts" xml:lang="{locale}">'
        f'<voice name="{voice}">'
        f'<mstts:viseme type="FacialExpression"/>'
        f"{_escape(text)}"
        f"</voice></speak>"
    )


class AzureSpeech:
    def __init__(self, cfg: Settings):
        self._cfg = cfg

    def ready(self) -> tuple[bool, str]:
        if not self._cfg.azure_configured:
            return False, ("NOT_CONFIGURED: no Azure key yet - stage 3 "
                           "(scripts/stage3_speech.sh)")
        try:
            import azure.cognitiveservices.speech  # noqa: F401
        except ImportError as exc:
            return False, f"NOT_INSTALLED: azure speech SDK is stage 3 ({exc})"
        return True, f"{self._cfg.azure_voice} @ {self._cfg.azure_region}"

    def synthesise(self, answer_id: str, text: str) -> tuple[bytes, VisemeTrackModel, int]:
        """Synthesise in a child process, because a failure here can be a segfault.

        Measured on macOS 26.6 with SDK 1.42: a null dereference inside
        CSpxAppleCodecAdapter::SetFormat, on a thread the SDK spawned, with no Python
        frames in the trace. In-process that kills the service mid-demo and no
        try/except can intervene. Out of process it costs one answer, the parent
        returns TTS_UNAVAILABLE, and Unity falls back to a recorded clip.

        The cost is one interpreter start plus SDK init per answer - small against a
        5-11 second reply, and worth it for a demo that degrades instead of dying.
        """
        if not self._cfg.azure_configured:
            raise ServiceError(ErrorCode.TTS_UNAVAILABLE, "Azure Speech is not configured.")

        # The worker returns through a file, not stdout: the Azure SDK's native
        # library writes its own diagnostics to stdout, and the first version of this
        # failed with "unreadable output" and an empty stderr because SDK noise had
        # been prefixed to the JSON. stdout and stderr are now diagnostics only.
        result_path = VAR_DIR / f"{answer_id}.worker.json"
        request = json.dumps({
            "text": text,
            "voice": self._cfg.azure_voice,
            "answerId": answer_id,
            "audioPath": str(AUDIO_DIR / f"{answer_id}.wav"),
            "resultPath": str(result_path),
        })

        # The key goes through the environment. argv is visible to anyone who can run
        # `ps` on the VX PC.
        env = {
            **os.environ,
            "AIHOLO_AZURE_KEY": self._cfg.azure_key,
            "AIHOLO_AZURE_REGION": self._cfg.azure_region,
        }

        started = time.perf_counter()
        try:
            try:
                proc = subprocess.run(
                    [sys.executable, "-m", "app.tts_worker"],
                    input=request, capture_output=True, text=True, env=env,
                    cwd=str(SERVICE_ROOT), timeout=self._cfg.tts_timeout_s,
                )
            except subprocess.TimeoutExpired as exc:
                raise ServiceError(
                    ErrorCode.TTS_UNAVAILABLE,
                    f"Speech worker did not finish within {self._cfg.tts_timeout_s}s.",
                ) from exc
            tts_ms = int((time.perf_counter() - started) * 1000)

            # A negative return code is death by signal - the segfault this exists for.
            if proc.returncode < 0:
                raise ServiceError(
                    ErrorCode.TTS_UNAVAILABLE,
                    f"Speech worker was killed by signal {-proc.returncode} "
                    "(a crash inside the Azure SDK, not a Python error). The answer "
                    "text is unaffected; the avatar should use a recorded clip.",
                    workerStdout=proc.stdout[-400:],
                    workerStderr=proc.stderr[-400:],
                )

            if not result_path.exists():
                # Whatever went wrong, the SDK's own output is the only clue - so it
                # is carried in the error rather than discarded.
                raise ServiceError(
                    ErrorCode.TTS_UNAVAILABLE,
                    f"Speech worker exited {proc.returncode} without writing a result.",
                    workerStdout=proc.stdout[-400:],
                    workerStderr=proc.stderr[-400:],
                )

            payload = json.loads(result_path.read_text(encoding="utf-8"))
            if "error" in payload:
                raise ServiceError(ErrorCode.TTS_UNAVAILABLE, payload["error"])

            wav_bytes = (AUDIO_DIR / f"{answer_id}.wav").read_bytes()
            return wav_bytes, VisemeTrackModel(**payload["track"]), tts_ms
        finally:
            result_path.unlink(missing_ok=True)


class MockSpeech:
    """Silent audio of a plausible length plus a gently moving jaw, so the whole
    Unity path - clip length, turn control, blendshape wiring - can be exercised
    before an Azure key exists. Obviously not a voice."""

    def __init__(self, cfg: Settings):
        self._cfg = cfg

    def ready(self) -> tuple[bool, str]:
        return True, "mock - silent audio, synthetic jaw track"

    def synthesise(self, answer_id: str, text: str) -> tuple[bytes, VisemeTrackModel, int]:
        words = max(len(text.split()), 1)
        seconds = min(max(words / 2.6, 1.0), 30.0)
        n_samples = int(seconds * SAMPLE_RATE)

        buf = BytesIO()
        with wave.open(buf, "wb") as w:
            w.setnchannels(1)
            w.setsampwidth(2)
            w.setframerate(SAMPLE_RATE)
            w.writeframes(struct.pack(f"<{n_samples}h", *([0] * n_samples)))

        frame_count = int(seconds * FPS)
        values: list[float] = []
        for f in range(frame_count):
            row = [0.0] * SHAPE_COUNT
            row[JAW_OPEN_INDEX] = 0.35 + 0.25 * math.sin(f * 0.8)
            values.extend(row)

        track = VisemeTrackModel(
            id=answer_id, fps=FPS, shapeCount=SHAPE_COUNT,
            frameCount=frame_count, values=values,
            visemeIds=[], visemeOffsetsMs=[],
        )
        return buf.getvalue(), track, 2


def build_tts(cfg: Settings):
    return MockSpeech(cfg) if cfg.mock else AzureSpeech(cfg)
