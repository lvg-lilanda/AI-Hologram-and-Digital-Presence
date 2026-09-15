"""T07 - Benchmark local speech-to-text options with lab audio.

Card acceptance criteria and how this produces the evidence for each:
  "At least two STT configurations use the same recorded samples."
        --configs takes model:compute pairs; all run over the same sample folder.
  "Transcription errors and response latency are recorded."
        word error rate against each sample's reference text, plus wall-clock and
        real-time factor.
  "Three speakers and typical VX-lab noise are represented."
        the sample folder must contain at least three speaker ids and at least one
        sample tagged noisy, or this script refuses to run - the coverage rule is
        enforced here rather than left to memory.

Recording the samples (do this in the VX lab, with the lab's own microphone and the
Looking Glass running so its fan is in the noise floor):

    service/bench/samples/
        ali_quiet_01.wav        ali_quiet_01.txt      <- reference transcript
        ali_noisy_01.wav        ali_noisy_01.txt
        hiba_quiet_01.wav       hiba_quiet_01.txt
        ...
    Filename pattern: <speaker>_<quiet|noisy>_<n>.wav, 16 kHz mono 16-bit PCM.

Usage:
    python -m bench.bench_stt --configs small.en:int8 medium.en:int8 base.en:int8
"""
from __future__ import annotations

import argparse
import platform
import re
import statistics
import time
import wave
from datetime import datetime
from pathlib import Path

HERE = Path(__file__).resolve().parent
SAMPLES = HERE / "samples"
RESULTS = HERE / "results"

MIN_SPEAKERS = 3


def normalise(text: str) -> list[str]:
    return re.sub(r"[^a-z0-9' ]", " ", text.lower()).split()


def word_error_rate(reference: str, hypothesis: str) -> float:
    """Levenshtein distance over words / reference length. The standard STT measure."""
    ref, hyp = normalise(reference), normalise(hypothesis)
    if not ref:
        return 0.0 if not hyp else 1.0
    prev = list(range(len(hyp) + 1))
    for i, r in enumerate(ref, 1):
        cur = [i]
        for j, h in enumerate(hyp, 1):
            cur.append(min(prev[j] + 1, cur[j - 1] + 1, prev[j - 1] + (r != h)))
        prev = cur
    return prev[-1] / len(ref)


def seconds_of(path: Path) -> float:
    with wave.open(str(path), "rb") as w:
        return w.getnframes() / float(w.getframerate())


def collect() -> list[tuple[Path, str, str, str]]:
    """Return (wav, reference, speaker, condition), refusing an unrepresentative set."""
    out = []
    for wav in sorted(SAMPLES.glob("*.wav")):
        ref = wav.with_suffix(".txt")
        if not ref.exists():
            print(f"  skipping {wav.name}: no matching .txt reference")
            continue
        parts = wav.stem.split("_")
        speaker = parts[0]
        condition = parts[1] if len(parts) > 1 else "unknown"
        out.append((wav, ref.read_text(encoding="utf-8").strip(), speaker, condition))
    return out


def main() -> int:
    p = argparse.ArgumentParser()
    p.add_argument("--configs", nargs="+", required=True,
                   help="model:compute pairs, e.g. small.en:int8 medium.en:int8")
    p.add_argument("--device", default="auto")
    args = p.parse_args()

    if len(args.configs) < 2:
        print("The card requires at least two configurations. Pass two or more.")
        return 1

    samples = collect()
    if not samples:
        print(f"No samples in {SAMPLES}. Record them first - see this file's docstring.")
        return 1

    speakers = {s for _, _, s, _ in samples}
    conditions = {c for _, _, _, c in samples}
    if len(speakers) < MIN_SPEAKERS:
        print(f"Only {len(speakers)} speaker(s): {sorted(speakers)}. "
              f"The card requires {MIN_SPEAKERS}.")
        return 1
    if "noisy" not in conditions:
        print("No sample tagged 'noisy'. The card requires typical VX-lab noise.")
        return 1

    from faster_whisper import WhisperModel

    RESULTS.mkdir(parents=True, exist_ok=True)
    stamp = datetime.now().strftime("%Y%m%d-%H%M")
    out = RESULTS / f"stt_{stamp}.md"

    lines = [
        f"# Local STT benchmark - {datetime.now():%Y-%m-%d %H:%M}",
        "",
        f"- Host: {platform.node()} ({platform.system()} {platform.release()})",
        f"- Samples: {len(samples)} across speakers {sorted(speakers)} "
        f"and conditions {sorted(conditions)}",
        "",
        "| Config | Median WER | Quiet WER | Noisy WER | Median latency (s) | Median RTF |",
        "|---|---:|---:|---:|---:|---:|",
    ]
    detail: list[str] = []

    for spec in args.configs:
        model_name, _, compute = spec.partition(":")
        compute = compute or "int8"
        print(f"\n== {spec}")
        try:
            model = WhisperModel(model_name, device=args.device, compute_type=compute)
        except Exception as exc:
            lines.append(f"| `{spec}` | load failed: {exc} | | | | |")
            continue

        wers, quiet, noisy, lats, rtfs = [], [], [], [], []
        detail.append(f"\n## {spec}\n")
        for wav, ref, speaker, condition in samples:
            audio_s = seconds_of(wav)
            started = time.perf_counter()
            segments, _ = model.transcribe(str(wav), language="en", vad_filter=True,
                                           beam_size=1)
            hyp = " ".join(s.text.strip() for s in segments).strip()
            elapsed = time.perf_counter() - started

            wer = word_error_rate(ref, hyp)
            wers.append(wer)
            (noisy if condition == "noisy" else quiet).append(wer)
            lats.append(elapsed)
            rtfs.append(elapsed / audio_s if audio_s else 0)
            detail.append(
                f"- `{wav.name}` ({speaker}, {condition}) WER {wer:.1%}, "
                f"{elapsed:.2f}s for {audio_s:.1f}s audio\n"
                f"  - ref: {ref}\n  - hyp: {hyp}\n"
            )
            print(f"   {wav.name:28} WER {wer:6.1%}  {elapsed:5.2f}s")

        med = statistics.median
        lines.append(
            f"| `{spec}` | {med(wers):.1%} | {med(quiet) if quiet else 0:.1%} "
            f"| {med(noisy) if noisy else 0:.1%} | {med(lats):.2f} | {med(rtfs):.2f} |"
        )

    lines += [
        "",
        "Real-time factor under 1.0 means the engine transcribes faster than the ",
        "audio arrives. Pick the cheapest configuration whose noisy WER is still ",
        "usable - the lab is never quiet, so the noisy column is the one that decides.",
        "",
        "| Config | Decision |", "|---|---|",
    ] + [f"| `{c}` |  |" for c in args.configs] + ["", "# Per-sample detail"] + detail

    out.write_text("\n".join(lines), encoding="utf-8")
    print(f"\nWrote {out}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
