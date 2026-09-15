"""Multi-turn evaluation. The harness that would have caught the hallucination.

    python -m bench.eval_conversation        # the service must be running

bench/eval_grounding.py asks every question cold. Nobody talks to a hologram that
way, and the difference matters: a follow-up like "ok tell me about the project"
carries almost no signal on its own, retrieves badly, and hands the model a gap it
will fill from training if nothing stops it. That is exactly what happened the first
time a person sat down with it, and no isolated-question test could have found it.

Each script in bench/conversations.json runs in its own session, so later turns
genuinely depend on earlier ones. A turn can also assert `must_not_contain` - words
that would only appear if the model reached outside the corpus.

Exit codes:
    0  every turn behaved
    2  at least one turn did not
    1  could not run
"""
from __future__ import annotations

import argparse
import json
import sys
import uuid
from datetime import datetime
from pathlib import Path

import httpx

HERE = Path(__file__).resolve().parent
SCRIPTS = HERE / "conversations.json"
RESULTS = HERE / "results"


def main() -> int:
    p = argparse.ArgumentParser()
    p.add_argument("--url", default="http://127.0.0.1:8765")
    p.add_argument("--timeout", type=float, default=180.0)
    args = p.parse_args()

    try:
        health = httpx.get(f"{args.url}/health", timeout=5).json()
    except Exception as exc:
        print(f"Service not reachable at {args.url}: {exc}\nStart it:  ./run.sh",
              file=sys.stderr)
        return 1
    if health.get("mock"):
        print("Service is in mock mode - this proves nothing about the real model.",
              file=sys.stderr)
        return 1

    data = json.loads(SCRIPTS.read_text(encoding="utf-8"))
    failures, lines = [], []

    for convo in data["conversations"]:
        session = f"conv-{uuid.uuid4().hex[:8]}"
        print(f"\n=== {convo['name']}")
        lines += [f"## {convo['name']}", ""]
        if convo.get("note"):
            lines += [f"*{convo['note']}*", ""]

        for i, turn in enumerate(convo["turns"], 1):
            said, expected = turn["say"], turn["expect"]
            try:
                r = httpx.post(f"{args.url}/answer",
                               json={"text": said, "sessionId": session, "speak": False},
                               timeout=args.timeout)
                d = r.json()
            except Exception as exc:
                print(f"  ERROR  turn {i}: {exc}")
                failures.append((convo["name"], i, said, f"request failed: {exc}"))
                continue

            if "error" in d:
                code = d["error"]["code"]
                print(f"  ERROR  turn {i}: {code}")
                failures.append((convo["name"], i, said, code))
                continue

            got = d.get("outcome", "answered" if d.get("grounded") else "not_found")
            text = d.get("text", "")
            problems = []
            if got != expected:
                problems.append(f"expected {expected}, got {got}")
            for banned in turn.get("must_not_contain", []):
                if banned.lower() in text.lower():
                    problems.append(f"said '{banned}' - not in the corpus")

            mark = "ok  " if not problems else "FAIL"
            print(f"  {mark}  {i}. {said}")
            print(f"        -> [{got}] {text[:150]}")
            for prob in problems:
                print(f"        !! {prob}")
                failures.append((convo["name"], i, said, prob))

            cites = ", ".join(f"{c['title']} / {c.get('section') or c.get('page')}"
                              for c in d.get("citations", [])) or "none"
            lines += [
                f"**{i}. {said}**",
                "",
                f"- outcome: {got} (expected {expected}){'  — **WRONG**' if problems else ''}",
                f"- cited: {cites}",
                "",
                f"> {text}",
                "",
            ]
            for prob in problems:
                lines += [f"- problem: {prob}", ""]

    total = sum(len(c["turns"]) for c in data["conversations"])
    print("\n" + "-" * 62)
    print(f"  {total - len(failures)}/{total} turns behaved")
    if failures:
        print("\nFAILURES:")
        for name, i, said, why in failures:
            print(f"  [{name}] turn {i} \"{said}\"\n      {why}")

    RESULTS.mkdir(parents=True, exist_ok=True)
    out = RESULTS / f"conversation_eval_{datetime.now():%Y%m%d-%H%M}.md"
    out.write_text("\n".join(
        [f"# Multi-turn evaluation - {datetime.now():%Y-%m-%d %H:%M}", "",
         f"- Model: `{health.get('model', {}).get('detail')}`",
         f"- Index: {health.get('index', {}).get('detail')}",
         f"- Turns: {total - len(failures)}/{total} behaved", ""] + lines),
        encoding="utf-8")
    print(f"\nWrote {out}")
    return 2 if failures else 0


if __name__ == "__main__":
    raise SystemExit(main())
