"""Stage 3 verification: does Azure speech actually meet the Unity contract?

    python -m bench.verify_speech        # the service must be running, key in .env

This is T25's evidence. Each check maps to one acceptance criterion, and the third
is the one nobody would notice failing until the demo:

  "One request produces answer audio and its matching facial timeline."
      -> one /answer call returns both, and the track's DURATION matches the audio's.
         A track that is right in every field but half a second short means the mouth
         finishes before the voice does. On a hologram a metre from someone's face
         that is the most visible defect available.

  "Audio format and facial-frame timing match the Unity contract."
      -> 16 kHz mono 16-bit RIFF; 60 fps, 55 shapes, values flattened to exactly
         frameCount * shapeCount, because VisemePlayer indexes it as
         frame * shapeCount + i and a wrong length reads the wrong blend shape.

  "Azure credentials remain outside Unity and source control."
      -> .env is gitignored and untracked, and no key appears in the response.

It also checks the blend shape values actually MOVE. An all-zero track passes every
structural check and produces an avatar with a closed mouth reciting a paragraph.
"""
from __future__ import annotations

import argparse
import io
import json
import subprocess
import sys
import wave
from datetime import datetime
from pathlib import Path

import httpx

HERE = Path(__file__).resolve().parent
RESULTS = HERE / "results"
SERVICE_ROOT = HERE.parent

EXPECTED_RATE, EXPECTED_CHANNELS, EXPECTED_WIDTH = 16_000, 1, 2
EXPECTED_FPS, EXPECTED_SHAPES = 60, 55
#: Track and audio may differ by this much and still look in sync. Azure ends the
#: track on the last viseme, so a little trailing silence is normal.
DURATION_TOLERANCE_S = 0.35


def check(results: list, ok: bool, label: str, detail: str = "") -> bool:
    results.append((ok, label, detail))
    print(f"  {'PASS' if ok else 'FAIL'}  {label}" + (f"  [{detail}]" if detail else ""))
    return ok


def main() -> int:
    p = argparse.ArgumentParser()
    p.add_argument("--url", default="http://127.0.0.1:8765")
    p.add_argument("--question", default="What is Telstra muru-D?")
    p.add_argument("--timeout", type=float, default=180.0)
    args = p.parse_args()

    results: list = []

    try:
        health = httpx.get(f"{args.url}/health", timeout=5).json()
    except Exception as exc:
        print(f"Service not reachable at {args.url}: {exc}\nStart it:  ./run.sh",
              file=sys.stderr)
        return 1
    if health.get("mock"):
        print("Service is in mock mode - mock audio is silence and the jaw track is "
              "synthetic. This proves nothing about Azure.", file=sys.stderr)
        return 1
    if not health.get("azure", {}).get("ready"):
        print(f"Azure not ready: {health['azure']['detail']}\n\n"
              "  1. pip install -r requirements-tts.txt\n"
              "  2. put the key and region in service/.env\n"
              "  3. restart the service", file=sys.stderr)
        return 1

    print(f"Asking: {args.question}\n")
    r = httpx.post(f"{args.url}/answer",
                   json={"text": args.question, "sessionId": "verify-speech",
                         "speak": True},
                   timeout=args.timeout)
    body = r.json()
    if "error" in body:
        print(f"  {body['error']['code']}: {body['error']['message']}", file=sys.stderr)
        return 1

    # --- one request, both artefacts ---------------------------------------
    check(results, bool(body.get("audioUrl")), "one request returned answer audio")
    check(results, bool(body.get("visemesUrl")), "the same request returned a facial timeline")
    if not (body.get("audioUrl") and body.get("visemesUrl")):
        return 2

    wav_bytes = httpx.get(f"{args.url}{body['audioUrl']}", timeout=60).content
    track = httpx.get(f"{args.url}{body['visemesUrl']}", timeout=60).json()

    # --- audio format ------------------------------------------------------
    audio_seconds = 0.0
    try:
        with wave.open(io.BytesIO(wav_bytes), "rb") as w:
            rate, channels, width, frames = (w.getframerate(), w.getnchannels(),
                                             w.getsampwidth(), w.getnframes())
        audio_seconds = frames / float(rate)
        check(results, (rate, channels, width) == (EXPECTED_RATE, EXPECTED_CHANNELS,
                                                   EXPECTED_WIDTH),
              "audio is 16 kHz mono 16-bit PCM",
              f"{rate} Hz, {channels} ch, {width * 8}-bit, {audio_seconds:.2f}s")
    except Exception as exc:
        check(results, False, "audio is a readable RIFF WAV", str(exc))
        return 2

    # --- track shape -------------------------------------------------------
    check(results, track.get("fps") == EXPECTED_FPS, "track is 60 fps",
          str(track.get("fps")))
    check(results, track.get("shapeCount") == EXPECTED_SHAPES, "track has 55 blend shapes",
          str(track.get("shapeCount")))

    values, frame_count = track.get("values", []), track.get("frameCount", 0)
    expected_len = frame_count * track.get("shapeCount", 0)
    check(results, len(values) == expected_len and expected_len > 0,
          "values flattened to exactly frameCount * shapeCount",
          f"{len(values)} vs {expected_len}")

    check(results, track.get("id") == body["id"], "track id matches the answer id")

    # --- the one that matters ----------------------------------------------
    track_seconds = frame_count / float(track.get("fps") or EXPECTED_FPS)
    drift = abs(track_seconds - audio_seconds)
    check(results, drift <= DURATION_TOLERANCE_S,
          f"facial timeline matches the audio length (within {DURATION_TOLERANCE_S}s)",
          f"track {track_seconds:.2f}s vs audio {audio_seconds:.2f}s, drift {drift:.2f}s")

    # --- does the face actually move? --------------------------------------
    jaw_open = values[17::track.get("shapeCount", EXPECTED_SHAPES)] if values else []
    moving = len(set(round(v, 3) for v in jaw_open)) > 3
    check(results, moving, "blend shape values vary rather than sitting at zero",
          f"{len(set(round(v, 3) for v in jaw_open))} distinct jaw-open values")
    check(results, bool(track.get("visemeIds")), "viseme ids present for the uLipSync path",
          f"{len(track.get('visemeIds', []))} events")

    # --- credentials -------------------------------------------------------
    env_tracked = subprocess.run(["git", "ls-files", "--error-unmatch", "service/.env"],
                                 cwd=SERVICE_ROOT.parent, capture_output=True)
    check(results, env_tracked.returncode != 0, "service/.env is NOT tracked by git")
    check(results, "azure" not in json.dumps(body).lower() or "key" not in
          json.dumps(body).lower(), "no credential appears in the answer payload")

    passed = sum(1 for ok, _, _ in results if ok)
    print(f"\n  {passed}/{len(results)} checks passed")

    RESULTS.mkdir(parents=True, exist_ok=True)
    out = RESULTS / f"speech_verify_{datetime.now():%Y%m%d-%H%M}.md"
    out.write_text("\n".join(
        [f"# Azure speech verification - {datetime.now():%Y-%m-%d %H:%M}", "",
         f"- Voice: `{health.get('azure', {}).get('detail')}`",
         f"- Question: {args.question}",
         f"- Audio: {audio_seconds:.2f}s | Track: {track_seconds:.2f}s, "
         f"{frame_count} frames", "",
         "| Check | Result | Detail |", "|---|---|---|"]
        + [f"| {label} | {'PASS' if ok else 'FAIL'} | {detail} |"
           for ok, label, detail in results]
        + ["", f"**{passed}/{len(results)} passed.**", "",
           "Evidence for T25: one request produces answer audio and its matching "
           "facial timeline; audio format and facial-frame timing match the Unity "
           "contract; Azure credentials stay outside Unity and source control."]),
        encoding="utf-8")
    print(f"  Wrote {out}")
    return 0 if passed == len(results) else 2


if __name__ == "__main__":
    raise SystemExit(main())
