"""Does the avatar actually refuse what it should? Measured end to end.

This is the number that matters, and it is not the retrieval score. A question goes
through the whole pipeline - retrieval, the grounding floor, the model's own
answered/out_of_scope/not_found classification - and the only thing recorded is what
the service finally did.

    python -m bench.eval_grounding          # the service must be running

Four outcomes, and they are not equally bad:

    answerable   -> answered   correct
    unanswerable -> refused    correct
    answerable   -> refused    a gap in the corpus; annoying, safe
    unanswerable -> answered   the one that matters - the avatar invented something
                               in front of the client

Exit code is non-zero if anything lands in that last box.

Evidence for T15's "unsupported questions return an explicit not-found response",
and the harness Anuji's 20-question set (T08) plugs into - replace the lists in
bench/grounding_questions.json and rerun.
"""
from __future__ import annotations

import argparse
import json
import sys
from datetime import datetime
from pathlib import Path

import httpx

HERE = Path(__file__).resolve().parent
QUESTIONS = HERE / "grounding_questions.json"
RESULTS = HERE / "results"


def ask(base: str, question: str, timeout: float) -> dict:
    try:
        r = httpx.post(f"{base}/answer",
                       json={"text": question, "sessionId": "eval", "speak": False},
                       timeout=timeout)
    except Exception as exc:
        return {"error": f"request failed: {exc}"}
    if r.status_code >= 400:
        body = r.json().get("error", {}) if r.headers.get("content-type", "").startswith("application/json") else {}
        return {"error": f"{body.get('code', r.status_code)}: {body.get('message', r.text[:120])}"}
    return r.json()


def main() -> int:
    p = argparse.ArgumentParser()
    p.add_argument("--url", default="http://127.0.0.1:8765")
    p.add_argument("--timeout", type=float, default=120.0)
    args = p.parse_args()

    try:
        health = httpx.get(f"{args.url}/health", timeout=5).json()
    except Exception as exc:
        print(f"Service not reachable at {args.url}: {exc}\n"
              f"Start it first:  ./run.sh", file=sys.stderr)
        return 1
    if health.get("mock"):
        print("Service is in mock mode - this measures the mock matcher, not the real "
              "pipeline. Unset AIHOLO_MOCK and restart it.", file=sys.stderr)
        return 1
    if not health.get("model", {}).get("ready"):
        print(f"Model not ready: {health.get('model', {}).get('detail')}", file=sys.stderr)
        return 1

    data = json.loads(QUESTIONS.read_text(encoding="utf-8"))
    cases = ([(q, True) for q in data["answerable"]]
             + [(q, False) for q in data["unanswerable"]])

    rows, errors = [], []
    print(f"Asking {len(cases)} questions through {args.url}\n")
    for question, should_answer in cases:
        result = ask(args.url, question, args.timeout)
        if "error" in result:
            errors.append((question, result["error"]))
            print(f"  ERROR  {question}\n         {result['error']}")
            continue
        # A helpful out-of-scope reply is a correct refusal, not an invention.
        outcome = result.get("outcome", "answered" if result.get("grounded") else "not_found")
        answered = outcome == "answered"
        correct = answered == should_answer
        rows.append({
            "question": question, "should_answer": should_answer,
            "answered": answered, "correct": correct, "outcome": outcome,
            "text": result.get("text", ""),
            "citations": result.get("citations", []),
            "ms": result.get("timings", {}).get("totalMs", 0),
        })
        mark = "ok " if correct else ("MISS" if should_answer else "RISK")
        print(f"  {mark}  {outcome:<12}  "
              f"{result.get('timings', {}).get('totalMs', 0):>6} ms  {question}")

    true_pos  = [r for r in rows if r["should_answer"] and r["answered"]]
    false_neg = [r for r in rows if r["should_answer"] and not r["answered"]]
    true_neg  = [r for r in rows if not r["should_answer"] and not r["answered"]]
    false_pos = [r for r in rows if not r["should_answer"] and r["answered"]]

    print("\n" + "-" * 62)
    print(f"  answerable   answered   {len(true_pos):>3}   correct")
    print(f"  answerable   refused    {len(false_neg):>3}   gap in the corpus")
    oos = len([r for r in true_neg if r["outcome"] == "out_of_scope"])
    print(f"  unanswerable refused    {len(true_neg):>3}   correct"
          + (f"  ({oos} via the out-of-scope section)" if oos else ""))
    print(f"  unanswerable ANSWERED   {len(false_pos):>3}   invented an answer")
    if errors:
        print(f"  errors                  {len(errors):>3}")
    total = len(rows) or 1
    print(f"\n  correct: {len(true_pos) + len(true_neg)}/{total}")

    if false_pos:
        print("\nTHE ONES THAT MATTER - answered when it should have refused:")
        for r in false_pos:
            print(f"\n  Q: {r['question']}")
            print(f"  A: {r['text'][:220]}")
            print(f"  cited: {[(c['title'], c.get('section') or c.get('page')) for c in r['citations']]}")
        print("\n  Fix the corpus first - an explicit out-of-scope passage that covers "
              "these\n  topics gives the model something correct to retrieve. Raising the "
              "floor\n  would only start refusing real questions too.")

    if false_neg:
        print("\nRefused questions it should answer (add the material to the corpus):")
        for r in false_neg:
            print(f"  {r['question']}")

    RESULTS.mkdir(parents=True, exist_ok=True)
    out = RESULTS / f"grounding_eval_{datetime.now():%Y%m%d-%H%M}.md"
    lines = [
        f"# End-to-end grounding evaluation - {datetime.now():%Y-%m-%d %H:%M}",
        "",
        f"- Model: `{health.get('model', {}).get('detail')}`",
        f"- Index: {health.get('index', {}).get('detail')}",
        f"- Questions: {len(cases)}",
        "",
        "| Outcome | Count | Meaning |",
        "|---|---:|---|",
        f"| answerable → answered | {len(true_pos)} | correct |",
        f"| answerable → refused | {len(false_neg)} | gap in the corpus |",
        f"| unanswerable → refused | {len(true_neg)} | correct |",
        f"| unanswerable → **answered** | {len(false_pos)} | **invented an answer** |",
        "",
        "## Every answer",
        "",
    ]
    for r in rows:
        cites = ", ".join(f"{c['title']} / {c.get('section') or c.get('page')}"
                          for c in r["citations"]) or "none"
        lines += [
            f"### {r['question']}",
            "",
            f"- expected: {'answer' if r['should_answer'] else 'refuse'} — "
            f"got: {r['outcome']} — "
            f"{'correct' if r['correct'] else '**WRONG**'} ({r['ms']} ms)",
            f"- cited: {cites}",
            "",
            f"> {r['text']}",
            "",
        ]
    out.write_text("\n".join(lines), encoding="utf-8")
    print(f"\nWrote {out}")

    if errors:
        return 1
    return 2 if false_pos else 0


if __name__ == "__main__":
    raise SystemExit(main())
