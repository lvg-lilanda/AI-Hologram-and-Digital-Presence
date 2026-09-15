"""T27 — end-to-end latency, broken down by stage.

    python -m bench.bench_latency            # service must be running

Runs complete turns through /answer with speech on, and records where the time
actually goes: retrieval, model inference, speech synthesis. Guessing which stage is
slow is how you end up optimising the one that costs 30 ms.

The first turn is reported separately. On the Mac it was 10.9 s against a ~5 s median
because Ollama loads the model into memory on first use. That is a real number for a
demo — the first visitor of the day waits twice as long as everyone else — but
averaging it in hides both facts.

Card criteria this produces evidence for:
  "At least ten complete turns are measured on the VX PC."
  "Release-to-transcript, answer and first-audio times are recorded."
        retrieve / infer / tts are recorded separately per turn.
  "Unity frame rate and AI-service resource use are recorded together."
        RAM and VRAM are sampled here; note the Unity frame rate by hand from the
        editor's Stats panel while this runs, and record both in the same session.
"""
from __future__ import annotations

import argparse
import json
import platform
import shutil
import statistics
import subprocess
import sys
from datetime import datetime
from pathlib import Path

import httpx

HERE = Path(__file__).resolve().parent
RESULTS = HERE / "results"
QUESTIONS = HERE / "grounding_questions.json"


def vram() -> str:
    if not shutil.which("nvidia-smi"):
        return "n/a (no nvidia-smi — CPU or Apple Silicon)"
    try:
        out = subprocess.run(
            ["nvidia-smi", "--query-gpu=memory.used,memory.total",
             "--format=csv,noheader,nounits"],
            capture_output=True, text=True, timeout=10).stdout.strip().splitlines()[0]
        used, total = (x.strip() for x in out.split(","))
        return f"{used} / {total} MB"
    except Exception as exc:
        return f"n/a ({exc})"


def ram() -> str:
    try:
        import psutil

        m = psutil.virtual_memory()
        return f"{(m.total - m.available) // 1_048_576} / {m.total // 1_048_576} MB"
    except ImportError:
        return "n/a (pip install psutil)"


def pct(values: list[float], p: float) -> float:
    if not values:
        return 0.0
    ordered = sorted(values)
    idx = min(int(round((p / 100) * (len(ordered) - 1))), len(ordered) - 1)
    return ordered[idx]


def main() -> int:
    p = argparse.ArgumentParser()
    p.add_argument("--url", default="http://127.0.0.1:8765")
    p.add_argument("--turns", type=int, default=12,
                   help="Total turns. The card asks for at least ten.")
    p.add_argument("--timeout", type=float, default=180.0)
    p.add_argument("--no-speech", action="store_true",
                   help="Skip synthesis to isolate retrieval and inference.")
    p.add_argument("--note", default="", help="e.g. 'Unity running on the Looking Glass, 72 fps'")
    args = p.parse_args()

    try:
        health = httpx.get(f"{args.url}/health", timeout=5).json()
    except Exception as exc:
        print(f"Service not reachable at {args.url}: {exc}\nStart it:  ./run.sh",
              file=sys.stderr)
        return 1
    if health.get("mock"):
        print("Service is in mock mode — these numbers would be meaningless.",
              file=sys.stderr)
        return 1

    speak = not args.no_speech
    if speak and not health.get("azure", {}).get("ready"):
        print("Azure is not ready, so no synthesis will happen. Either run stage 3 "
              "or pass --no-speech so the report says so honestly.", file=sys.stderr)
        return 1

    # Cycle the answerable questions so every turn is a real, grounded answer -
    # a refusal short-circuits inference and would flatter the numbers.
    pool = json.loads(QUESTIONS.read_text(encoding="utf-8"))["answerable"]
    ram_before, vram_before = ram(), vram()

    rows: list[dict] = []
    print(f"{args.turns} turns through {args.url}"
          f"{'' if speak else ' (speech off)'}\n")
    print(f"{'#':>3}  {'retrieve':>9} {'infer':>8} {'tts':>8} {'total':>8}   question")

    for i in range(args.turns):
        question = pool[i % len(pool)]
        try:
            r = httpx.post(f"{args.url}/answer",
                           json={"text": question, "sessionId": f"latency-{i}",
                                 "speak": speak},
                           timeout=args.timeout)
            body = r.json()
        except Exception as exc:
            print(f"{i + 1:>3}  request failed: {exc}")
            continue
        if "error" in body:
            print(f"{i + 1:>3}  {body['error']['code']}")
            continue

        t = body["timings"]
        rows.append({**t, "question": question, "cold": i == 0,
                     "outcome": body.get("outcome")})
        print(f"{i + 1:>3}  {t['retrieveMs']:>7} ms {t['inferMs']:>6} ms "
              f"{t['ttsMs']:>6} ms {t['totalMs']:>6} ms   {question[:40]}"
              f"{'   <- cold start' if i == 0 else ''}")

    if not rows:
        print("\nNo successful turns.", file=sys.stderr)
        return 1

    cold = rows[0]
    warm = rows[1:] or rows          # fall back if only one turn completed
    totals = [r["totalMs"] for r in warm]

    def stage(key: str) -> tuple[float, float]:
        vals = [r[key] for r in warm]
        return statistics.median(vals), pct(vals, 90)

    print("\n" + "-" * 66)
    print(f"  cold start (turn 1)   {cold['totalMs']:>6} ms")
    print(f"  warm turns measured   {len(warm):>6}")
    print(f"\n  {'stage':<12} {'median':>9} {'p90':>9}   share of median total")
    med_total = statistics.median(totals)
    for key, label in (("retrieveMs", "retrieval"), ("inferMs", "inference"),
                       ("ttsMs", "speech")):
        med, p90 = stage(key)
        share = (med / med_total * 100) if med_total else 0
        print(f"  {label:<12} {med:>7.0f} ms {p90:>7.0f} ms   {share:>5.1f}%")
    print(f"  {'TOTAL':<12} {med_total:>7.0f} ms {pct(totals, 90):>7.0f} ms")
    print(f"\n  fastest {min(totals)} ms   slowest {max(totals)} ms")

    slowest = max((("retrieval", stage("retrieveMs")[0]),
                   ("inference", stage("inferMs")[0]),
                   ("speech", stage("ttsMs")[0])), key=lambda x: x[1])
    print(f"\n  Dominant cost: {slowest[0]} at {slowest[1]:.0f} ms median.")
    if med_total > 3000:
        print("  Above 3 s the avatar needs the Thinking clip, or the pause reads")
        print("  as a freeze. That is a Unity change, not a service one.")

    RESULTS.mkdir(parents=True, exist_ok=True)
    out = RESULTS / f"latency_{datetime.now():%Y%m%d-%H%M}.md"
    lines = [
        f"# End-to-end latency — {datetime.now():%Y-%m-%d %H:%M}", "",
        f"- Host: {platform.node()} ({platform.system()} {platform.release()}, "
        f"{platform.machine()})",
        f"- Model: `{health.get('model', {}).get('detail')}`",
        f"- Index: {health.get('index', {}).get('detail')}",
        f"- Speech: {'on — ' + str(health.get('azure', {}).get('detail')) if speak else 'OFF'}",
        f"- RAM before / after: {ram_before} / {ram()}",
        f"- VRAM before / after: {vram_before} / {vram()}",
        f"- Operator note: {args.note or '(record the Unity frame rate here)'}",
        "",
        f"**Cold start (first turn): {cold['totalMs']} ms.** Ollama loads the model on "
        "first use, so the first visitor of the day waits roughly twice as long.",
        "",
        f"## {len(warm)} warm turns", "",
        "| Stage | Median | p90 | Share |", "|---|---:|---:|---:|",
    ]
    for key, label in (("retrieveMs", "Retrieval"), ("inferMs", "Inference"),
                       ("ttsMs", "Speech")):
        med, p90 = stage(key)
        lines.append(f"| {label} | {med:.0f} ms | {p90:.0f} ms | "
                     f"{(med / med_total * 100) if med_total else 0:.1f}% |")
    lines += [
        f"| **Total** | **{med_total:.0f} ms** | **{pct(totals, 90):.0f} ms** | |", "",
        f"Fastest {min(totals)} ms, slowest {max(totals)} ms. Dominant cost: "
        f"**{slowest[0]}**.", "", "## Every turn", "",
        "| # | Question | Retrieve | Infer | Speech | Total |", "|---:|---|---:|---:|---:|---:|",
    ]
    for i, r in enumerate(rows, 1):
        lines.append(f"| {i}{' (cold)' if r['cold'] else ''} | {r['question']} | "
                     f"{r['retrieveMs']} ms | {r['inferMs']} ms | {r['ttsMs']} ms | "
                     f"{r['totalMs']} ms |")
    out.write_text("\n".join(lines), encoding="utf-8")
    print(f"\nWrote {out}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
