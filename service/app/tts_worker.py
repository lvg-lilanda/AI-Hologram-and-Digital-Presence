"""Azure synthesis, in a throwaway process.

Run as `python -m app.tts_worker`, reading one JSON request on stdin and writing its
result to the file named in that request. Never imported by the service.

The result does NOT go to stdout. The Azure SDK's native library writes its own
diagnostics there, which arrive interleaved with anything we print - the first
version of this worker used stdout and failed with "produced unreadable output" and
an empty stderr, because the SDK had prefixed the JSON with a line of its own. A
file is not a shared channel; stdout and stderr are left entirely to whatever the
SDK wants to say, and are captured for diagnostics.

Why a subprocess rather than a function call:

    Thread 14 Crashed:
    libMicrosoft.CognitiveServices.Speech.core.dylib
        CSpxAppleCodecAdapter::SetFormat(SPXWAVEFORMATEX const*)
    libsystem_pthread.dylib  _pthread_start

That is from the crash report of 14 September 2026. The fault is a null dereference
inside the SDK's own native codec adapter, on a thread the SDK spawned - no Python
frames anywhere in the trace. Nothing in Python can catch it: the process simply
dies, taking the running service with it. A try/except cannot help, and neither can
being careful about callbacks.

So synthesis runs where a crash costs only the answer. The parent sees a negative
exit status, reports TTS_UNAVAILABLE, and Unity's FallbackAnswerSource plays a
recorded clip. The avatar keeps talking. On a lab PC in front of a client, a demo
that degrades beats a demo that stops.

The credential arrives through the environment, never argv - argv is visible to
anyone who runs `ps`.
"""
from __future__ import annotations

import json
import os
import sys
import wave

SHAPE_COUNT = 55
FPS = 60


def _escape(text: str) -> str:
    return (text.replace("&", "&amp;").replace("<", "&lt;").replace(">", "&gt;")
                .replace('"', "&quot;"))


def emit(result_path: str, payload: dict) -> None:
    """The only way this worker returns anything."""
    with open(result_path, "w", encoding="utf-8") as handle:
        json.dump(payload, handle)


def main() -> int:
    request = json.loads(sys.stdin.read())
    result_path = request["resultPath"]
    key = os.environ.get("AIHOLO_AZURE_KEY", "")
    region = os.environ.get("AIHOLO_AZURE_REGION", "")
    voice = request["voice"]
    text = request["text"]
    audio_path = request["audioPath"]

    if not (key and region):
        emit(result_path, {"error": "Azure key or region missing in the worker env"})
        return 1

    import azure.cognitiveservices.speech as speechsdk

    config = speechsdk.SpeechConfig(subscription=key, region=region)
    config.speech_synthesis_voice_name = voice
    config.set_speech_synthesis_output_format(
        speechsdk.SpeechSynthesisOutputFormat.Riff16Khz16BitMonoPcm
    )
    synthesiser = speechsdk.SpeechSynthesizer(speech_config=config, audio_config=None)

    frames: list[list[float]] = []
    viseme_ids: list[int] = []
    offsets_ms: list[float] = []

    def on_viseme(evt):
        viseme_ids.append(int(evt.viseme_id))
        offsets_ms.append(evt.audio_offset / 10_000.0)
        if not evt.animation:
            return
        try:
            payload = json.loads(evt.animation)
        except json.JSONDecodeError:
            return
        start = int(payload.get("FrameIndex", 0))
        blocks = payload.get("BlendShapes") or []
        if start > len(frames):
            frames.extend([[0.0] * SHAPE_COUNT] * (start - len(frames)))
        for i, block in enumerate(blocks):
            row = [float(v) for v in block][:SHAPE_COUNT]
            row += [0.0] * (SHAPE_COUNT - len(row))
            idx = start + i
            if idx < len(frames):
                frames[idx] = row
            else:
                frames.append(row)

    synthesiser.viseme_received.connect(on_viseme)

    ssml = (
        f'<speak version="1.0" xmlns="http://www.w3.org/2001/10/synthesis" '
        f'xmlns:mstts="http://www.w3.org/2001/mstts" xml:lang="en-AU">'
        f'<voice name="{voice}"><mstts:viseme type="FacialExpression"/>'
        f"{_escape(text)}</voice></speak>"
    )
    result = synthesiser.speak_ssml_async(ssml).get()

    if result.reason != speechsdk.ResultReason.SynthesizingAudioCompleted:
        detail = getattr(result, "cancellation_details", None)
        emit(result_path, {
            "error": f"Azure synthesis failed: "
                     f"{getattr(detail, 'error_details', result.reason)}"})
        return 1

    with open(audio_path, "wb") as handle:
        handle.write(bytes(result.audio_data))

    # Sanity-check our own output before the parent trusts it.
    with wave.open(audio_path, "rb") as w:
        if (w.getframerate(), w.getnchannels(), w.getsampwidth()) != (16_000, 1, 2):
            emit(result_path, {"error": "Azure returned an unexpected audio format"})
            return 1

    emit(result_path, {
        "track": {
            "id": request["answerId"],
            "fps": FPS,
            "shapeCount": SHAPE_COUNT,
            "frameCount": len(frames),
            "values": [v for row in frames for v in row],
            "visemeIds": viseme_ids,
            "visemeOffsetsMs": offsets_ms,
        }
    })
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
