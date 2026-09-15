"""T06 - Benchmark Ollama model candidates on the VX PC.

Card acceptance criteria and how this produces the evidence for each:
  "At least two Ollama models answer the same evaluation subset."
        --models takes a list; every model answers every question in the same file.
  "Answer quality, first-token time, RAM and VRAM are recorded."
        streamed so first-token time is real; RAM via psutil, VRAM via nvidia-smi;
        answers are written out in full so quality can be scored by a human.
  "The selected setup runs while Unity and Looking Glass are active."
        run this with the Unity player already running on the Looking Glass, then
        record that in the header - the script cannot verify it for you.

Usage (on the VX PC, Unity already running):
    python -m bench.bench_ollama --models llama3.1:8b-instruct-q4_K_M qwen2.5:7b-instruct-q4_K_M

Output: bench/results/ollama_<timestamp>.md  - paste the table into the Planner card.
"""
from __future__ import annotations

import argparse
import json
import platform
import shutil
import statistics
import subprocess
import time
from datetime import datetime
from pathlib import Path

import httpx

RESULTS = Path(__file__).resolve().parent / "results"

DEFAULT_QUESTIONS = [
    "What is Telstra muru-D?",
    "Why was this digital human created?",
    "What is the Looking Glass display used for in this project?",
    "What can you not help me with?",
    "Who is on the project team?",
]


def vram_mb() -> str:
    if not shutil.which("nvidia-smi"):
        return "n/a (no nvidia-smi)"
    try:
        out = subprocess.run(
            ["nvidia-smi", "--query-gpu=memory.used,memory.total",
             "--format=csv,noheader,nounits"],
            capture_output=True, text=True, timeout=10,
        ).stdout.strip().splitlines()[0]
        used, total = (x.strip() for x in out.split(","))
        return f"{used} / {total} MB"
    except Exception as exc:
        return f"n/a ({exc})"


def ram_mb() -> str:
    try:
        import psutil

        m = psutil.virtual_memory()
        return f"{(m.total - m.available) // 1_048_576} / {m.total // 1_048_576} MB"
    except ImportError:
        return "n/a (pip install psutil)"


def ask(url: str, model: str, question: str, timeout: float) -> dict:
    """One streamed call. Returns first-token and total latency plus the answer."""
    payload = {"model": model, "prompt": question, "stream": True,
               "options": {"temperature": 0.2, "num_predict": 200}}
    started = time.perf_counter()
    first_token = None
    chunks: list[str] = []
    try:
        with httpx.stream("POST", f"{url}/api/generate", json=payload,
                          timeout=timeout) as r:
            r.raise_for_status()
            for line in r.iter_lines():
                if not line:
                    continue
                piece = json.loads(line).get("response", "")
                if piece and first_token is None:
                    first_token = time.perf_counter() - started
                chunks.append(piece)
    except Exception as exc:
        return {"error": str(exc), "first_token_s": None, "total_s": None, "answer": ""}
    return {
        "error": "",
        "first_token_s": round(first_token or 0.0, 3),
        "total_s": round(time.perf_counter() - started, 3),
        "answer": "".join(chunks).strip(),
    }


def main() -> int:
    p = argparse.ArgumentParser()
    p.add_argument("--models", nargs="+", required=True)
    p.add_argument("--url", default="http://127.0.0.1:11434")
    p.add_argument("--timeout", type=float, default=90.0)
    p.add_argument("--questions", type=Path, default=None,
                   help="Text file, one question per line. Defaults to a built-in set.")
    p.add_argument("--note", default="",
                   help="e.g. 'Unity player running on Looking Glass throughout'")
    args = p.parse_args()

    if len(args.models) < 2:
        print("The card requires at least two models. Pass two or more.")
        return 1

    questions = (args.questions.read_text(encoding="utf-8").splitlines()
                 if args.questions else DEFAULT_QUESTIONS)
    questions = [q.strip() for q in questions if q.strip()]

    RESULTS.mkdir(parents=True, exist_ok=True)
    stamp = datetime.now().strftime("%Y%m%d-%H%M")
    out = RESULTS / f"ollama_{stamp}.md"

    lines = [
        f"# Ollama benchmark - {datetime.now():%Y-%m-%d %H:%M}",
        "",
        f"- Host: {platform.node()} ({platform.system()} {platform.release()})",
        f"- RAM at start: {ram_mb()}",
        f"- VRAM at start: {vram_mb()}",
        f"- Questions: {len(questions)}",
        f"- Operator note: {args.note or '(none - record whether Unity/Looking Glass was running)'}",
        "",
        "| Model | Answered | Median first token (s) | Median total (s) | RAM after | VRAM after |",
        "|---|---:|---:|---:|---|---|",
    ]
    transcripts: list[str] = []

    for model in args.models:
        print(f"\n== {model}")
        rows = []
        for q in questions:
            print(f"   {q[:60]}")
            rows.append((q, ask(args.url, model, q, args.timeout)))
        ok = [r for _, r in rows if not r["error"]]
        med_first = statistics.median([r["first_token_s"] for r in ok]) if ok else 0
        med_total = statistics.median([r["total_s"] for r in ok]) if ok else 0
        lines.append(
            f"| `{model}` | {len(ok)}/{len(rows)} | {med_first:.2f} | {med_total:.2f} "
            f"| {ram_mb()} | {vram_mb()} |"
        )
        transcripts.append(f"\n## {model}\n")
        for q, r in rows:
            transcripts.append(
                f"**Q:** {q}\n\n"
                + (f"*ERROR: {r['error']}*\n" if r["error"]
                   else f"first token {r['first_token_s']}s, total {r['total_s']}s\n\n"
                        f"> {r['answer']}\n")
            )

    lines += [
        "",
        "## Quality scoring - fill in by hand",
        "",
        "Score each answer 0-2: 0 wrong or invented, 1 usable with a correction, "
        "2 speakable as-is. The model this project wants is the one that refuses "
        "cleanly when it does not know, not the one that talks fastest.",
        "",
        "| Model | Quality /10 | Refused when it should | Decision |",
        "|---|---:|---|---|",
    ] + [f"| `{m}` |  |  |  |" for m in args.models] + ["", "# Transcripts"] + transcripts

    out.write_text("\n".join(lines), encoding="utf-8")
    print(f"\nWrote {out}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
