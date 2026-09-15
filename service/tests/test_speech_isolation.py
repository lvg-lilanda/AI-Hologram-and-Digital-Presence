"""Proof that a crash in the Azure SDK cannot take the service with it.

The real crash, from the report of 14 September 2026:

    Thread 14 Crashed:
    libMicrosoft.CognitiveServices.Speech.core.dylib
        CSpxAppleCodecAdapter::SetFormat(SPXWAVEFORMATEX const*)
    libsystem_pthread.dylib  _pthread_start

    exception: EXC_BAD_ACCESS (SIGSEGV), KERN_INVALID_ADDRESS at 0x4

No Python frames, so nothing could be caught. These tests do not need Azure - they
reproduce the shape of the failure, which is what the design has to survive.
"""
from __future__ import annotations

import os
import subprocess
import sys
from pathlib import Path

import pytest

# These tests build their Settings explicitly, so they neither need nor should touch
# AIHOLO_MOCK. Mutating os.environ at import time leaks across the whole pytest
# session and breaks whichever module imports next - which is exactly what it did.
from app.config import Settings  # noqa: E402
from app.errors import ErrorCode, ServiceError  # noqa: E402
from app.tts import AzureSpeech  # noqa: E402


def test_a_child_process_really_can_segfault_without_killing_us():
    """The premise. If this did not hold, isolating synthesis would prove nothing."""
    proc = subprocess.run(
        [sys.executable, "-c", "import ctypes; ctypes.string_at(4)"],
        capture_output=True,
    )
    assert proc.returncode < 0, "expected death by signal, got a clean exit"
    assert proc.returncode == -11, f"expected SIGSEGV (-11), got {proc.returncode}"
    # And the thing that matters: this test process is still alive to assert it.


def test_worker_killed_by_signal_becomes_a_typed_error(monkeypatch):
    """A segfaulting worker must surface as TTS_UNAVAILABLE, which Unity already
    knows means 'use a recorded clip'. Not a 500, and not a dead service."""
    speech = AzureSpeech(Settings(mock=False, azure_key="x" * 32,
                                  azure_region="australiaeast"))

    def fake_run(*args, **kwargs):
        return subprocess.CompletedProcess(args, returncode=-11, stdout="", stderr="")

    monkeypatch.setattr("app.tts.subprocess.run", fake_run)

    with pytest.raises(ServiceError) as exc:
        speech.synthesise("a1", "hello")
    detail = exc.value.detail["error"]
    assert detail["code"] == ErrorCode.TTS_UNAVAILABLE
    assert "signal 11" in detail["message"]


def test_worker_timeout_becomes_a_typed_error(monkeypatch):
    speech = AzureSpeech(Settings(mock=False, azure_key="x" * 32,
                                  azure_region="australiaeast"))

    def fake_run(*args, **kwargs):
        raise subprocess.TimeoutExpired(cmd="tts_worker", timeout=45)

    monkeypatch.setattr("app.tts.subprocess.run", fake_run)
    with pytest.raises(ServiceError) as exc:
        speech.synthesise("a1", "hello")
    assert exc.value.detail["error"]["code"] == ErrorCode.TTS_UNAVAILABLE


def test_the_key_is_never_passed_on_the_command_line(monkeypatch):
    """argv is visible to anyone who can run `ps` on the VX PC."""
    secret = "super-secret-key-value-9876543210"
    speech = AzureSpeech(Settings(mock=False, azure_key=secret,
                                  azure_region="australiaeast"))
    seen = {}

    def fake_run(cmd, **kwargs):
        seen["cmd"] = cmd
        seen["env"] = kwargs.get("env", {})
        seen["input"] = kwargs.get("input", "")
        return subprocess.CompletedProcess(cmd, returncode=-11, stdout="", stderr="")

    monkeypatch.setattr("app.tts.subprocess.run", fake_run)
    with pytest.raises(ServiceError):
        speech.synthesise("a1", "hello")

    assert secret not in " ".join(seen["cmd"])
    assert secret not in seen["input"]          # nor on stdin
    assert seen["env"]["AIHOLO_AZURE_KEY"] == secret   # only in the environment


def _tiny_wav() -> bytes:
    import io
    import struct
    import wave

    buf = io.BytesIO()
    with wave.open(buf, "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(16_000)
        w.writeframes(struct.pack("<1600h", *([0] * 1600)))
    return buf.getvalue()


def test_sdk_noise_on_stdout_does_not_corrupt_the_result(monkeypatch, tmp_path):
    """The bug this replaced. The worker used to return its JSON on stdout, which the
    Azure SDK's native library also writes to - so a single SDK diagnostic line turned
    a perfectly good synthesis into 'produced unreadable output' with an empty stderr.
    The result now comes back through a file, and stdout is left to the SDK entirely.
    """
    import json

    from app import tts

    monkeypatch.setattr(tts, "VAR_DIR", tmp_path)
    monkeypatch.setattr(tts, "AUDIO_DIR", tmp_path)

    speech = AzureSpeech(Settings(mock=False, azure_key="x" * 32,
                                  azure_region="australiaeast"))

    def fake_run(cmd, **kwargs):
        request = json.loads(kwargs["input"])
        Path(request["audioPath"]).write_bytes(_tiny_wav())
        Path(request["resultPath"]).write_text(json.dumps({
            "track": {"id": request["answerId"], "fps": 60, "shapeCount": 55,
                      "frameCount": 2, "values": [0.0] * 110,
                      "visemeIds": [1], "visemeOffsetsMs": [0.0]}
        }))
        # Exactly the failure mode: the SDK chatters on stdout.
        return subprocess.CompletedProcess(
            cmd, returncode=0,
            stdout="[INFO] SDK 1.42.0 initialising audio codec\nWARNING: ...\n",
            stderr="")

    monkeypatch.setattr("app.tts.subprocess.run", fake_run)

    wav, track, ms = speech.synthesise("a1", "hello there")
    assert wav[:4] == b"RIFF"
    assert track.shapeCount == 55 and track.frameCount == 2
    assert len(track.values) == track.frameCount * track.shapeCount


def test_a_worker_that_writes_no_result_reports_the_sdk_output(monkeypatch, tmp_path):
    """When it fails for a reason we have not seen yet, the SDK's own output is the
    only clue there is - so it travels with the error instead of being discarded."""
    from app import tts

    monkeypatch.setattr(tts, "VAR_DIR", tmp_path)
    monkeypatch.setattr(tts, "AUDIO_DIR", tmp_path)

    speech = AzureSpeech(Settings(mock=False, azure_key="x" * 32,
                                  azure_region="australiaeast"))

    def fake_run(cmd, **kwargs):
        return subprocess.CompletedProcess(cmd, returncode=1, stdout="codec table missing",
                                           stderr="libMicrosoft...: assertion failed")

    monkeypatch.setattr("app.tts.subprocess.run", fake_run)
    with pytest.raises(ServiceError) as exc:
        speech.synthesise("a1", "hello")

    detail = exc.value.detail["error"]
    assert detail["code"] == ErrorCode.TTS_UNAVAILABLE
    assert "codec table missing" in detail["workerStdout"]
    assert "assertion failed" in detail["workerStderr"]


def test_the_result_file_is_cleaned_up_even_when_synthesis_fails(monkeypatch, tmp_path):
    from app import tts

    monkeypatch.setattr(tts, "VAR_DIR", tmp_path)
    monkeypatch.setattr(tts, "AUDIO_DIR", tmp_path)
    speech = AzureSpeech(Settings(mock=False, azure_key="x" * 32,
                                  azure_region="australiaeast"))

    def fake_run(cmd, **kwargs):
        import json as _json
        request = _json.loads(kwargs["input"])
        Path(request["resultPath"]).write_text('{"error": "Azure said no"}')
        return subprocess.CompletedProcess(cmd, returncode=1, stdout="", stderr="")

    monkeypatch.setattr("app.tts.subprocess.run", fake_run)
    with pytest.raises(ServiceError):
        speech.synthesise("a1", "hello")
    assert list(tmp_path.glob("*.worker.json")) == [], "left a result file behind"
