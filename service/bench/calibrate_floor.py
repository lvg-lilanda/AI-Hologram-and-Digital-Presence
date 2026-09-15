"""Set AIHOLO_GROUNDING_FLOOR from measurement instead of from a guess.

The floor is a CHEAP PRE-FILTER, not the grounding decision. Measurement on the real
corpus showed why: with nomic-embed-text, "What is Telstra's share price today?"
scores 0.717 against a Telstra corpus while "Can I ask you anything I like?" scores
0.511. Both questions are topically about the same subject, so no absolute cosine
threshold separates them - embedding similarity measures aboutness, not answerability.

So the floor is set just below the weakest question the avatar SHOULD answer. Its job
is only to discard the genuinely unrelated. The actual refusal decision belongs to the
model, which classifies each reply as answered / out_of_scope / not_found - and to the
corpus, which carries an explicit "what I cannot help with"
section so near-miss questions retrieve a passage that addresses them directly.

Whether that combination actually works is measured end to end by bench/eval_grounding.py
against a running service. This script only sets the pre-filter.

    python -m bench.calibrate_floor

Prints the top retrieval score for every question in bench/grounding_questions.json,
reports the separation between the two sets, and recommends a floor. Rerun it after
changing the embedding model, and again on the VX PC with Anuji's 20-question set.

Exit codes are distinct so a caller can tell a real result from a crash:
    0  a workable floor was found
    2  some answerable question scores so low that no floor can keep it - the corpus
       is missing material, which is a finding rather than a crash
    1  could not run at all (no Ollama, mock mode, missing questions file)
"""
from __future__ import annotations

import json
import sys
from datetime import datetime
from pathlib import Path

HERE = Path(__file__).resolve().parent
QUESTIONS = HERE / "grounding_questions.json"
RESULTS = HERE / "results"


def main() -> int:
    from app.config import CORPUS_DIR, settings
    from app.rag import EmbeddingUnavailable, Retriever

    cfg = settings()
    if cfg.mock:
        print("AIHOLO_MOCK is true - mock mode matches lexically and has no cosine "
              "floor to calibrate. Unset it and start Ollama.", file=sys.stderr)
        return 1

    data = json.loads(QUESTIONS.read_text(encoding="utf-8"))
    answerable = data["answerable"]
    unanswerable = data["unanswerable"]

    retriever = Retriever(cfg)
    try:
        n = retriever.build(CORPUS_DIR)
    except EmbeddingUnavailable as exc:
        print(f"{exc}\n\nStart Ollama first:  ollama serve &", file=sys.stderr)
        return 1
    retriever._load()
    print(f"Indexed {n} chunks with {cfg.embed_model}\n")

    import numpy as np

    def top(q: str) -> float:
        v = retriever._encode([q], kind="query")[0]
        return float(np.max(retriever._vectors @ v))

    hits = [(q, top(q)) for q in answerable]
    misses = [(q, top(q)) for q in unanswerable]

    print("ANSWERABLE   (want above the floor)")
    for q, s in sorted(hits, key=lambda x: x[1]):
        print(f"  {s:.3f}  {q}")
    print("\nUNANSWERABLE (want below the floor)")
    for q, s in sorted(misses, key=lambda x: -x[1]):
        print(f"  {s:.3f}  {q}")

    lowest_hit = min(s for _, s in hits)
    highest_miss = max(s for _, s in misses)
    print(f"\nlowest answerable  : {lowest_hit:.3f}")
    print(f"highest unanswerable: {highest_miss:.3f}")

    lines = [
        f"# Grounding floor calibration - {datetime.now():%Y-%m-%d %H:%M}",
        "",
        f"- Embedding model: `{cfg.embed_model}`",
        f"- Corpus: {n} chunks",
        f"- Lowest answerable score: {lowest_hit:.3f}",
        f"- Highest unanswerable score: {highest_miss:.3f}",
        "",
    ]

    # The floor keeps every answerable question, with a small margin so a corpus
    # edit does not immediately push the weakest one under. Anything it also happens
    # to reject is a bonus, not the mechanism.
    MARGIN = 0.03
    ABSOLUTE_MIN = 0.30   # below this, nothing in the corpus is plausibly related
    recommended = max(round(lowest_hit - MARGIN, 2), ABSOLUTE_MIN)
    pre_filtered = [(q, sc) for q, sc in misses if sc < recommended]
    passed_through = [(q, sc) for q, sc in misses if sc >= recommended]

    print(f"\nRecommended:  AIHOLO_GROUNDING_FLOOR={recommended}")
    print(f"  keeps all {len(hits)} answerable questions "
          f"(margin {lowest_hit - recommended:.3f} on the weakest)")
    print(f"  rejects {len(pre_filtered)}/{len(misses)} unanswerable questions outright")

    if passed_through:
        print(f"\n  {len(passed_through)} unanswerable question(s) score above the floor:")
        for q, sc in sorted(passed_through, key=lambda x: -x[1]):
            print(f"    {sc:.3f}  {q}")
        print("\n  This is expected, not a bug. Embedding similarity measures whether a")
        print("  question is ABOUT the corpus, not whether the corpus ANSWERS it, and")
        print("  these are all about Telstra. Refusing them is the model's job - it")
        print("  classifies each reply as answered / out_of_scope / not_found - helped")
        print("  by the corpus's 'what I cannot help with' section. Measure whether")
        print("  that actually happens:")
        print("\n      python -m bench.eval_grounding      # needs the service running")

    if lowest_hit < ABSOLUTE_MIN:
        weak = [(q, sc) for q, sc in hits if sc < ABSOLUTE_MIN]
        print(f"\nPROBLEM: {len(weak)} answerable question(s) score below {ABSOLUTE_MIN}, "
              "so no sensible floor keeps them:")
        for q, sc in weak:
            print(f"  {sc:.3f}  {q}")
        print("The corpus is missing the material behind these. Add it, then rerun.")
        verdict = 2
    else:
        verdict = 0

    lines += [
        f"    AIHOLO_GROUNDING_FLOOR={recommended}",
        "",
        f"Keeps all {len(hits)} answerable questions; rejects {len(pre_filtered)} of "
        f"{len(misses)} unanswerable ones outright.",
        "",
        "The floor is a pre-filter, not the grounding decision - embedding similarity "
        "measures aboutness, not answerability, so questions that are about Telstra "
        "but unanswerable score high. The model's own answered/out_of_scope/not_found "
        "classification and the corpus's "
        "out-of-scope section handle those, and `bench/eval_grounding.py` measures "
        "whether they do.",
        "",
    ]
    if passed_through:
        lines += ["Unanswerable questions above the floor (left to the model):", ""] + \
                 [f"- {sc:.3f} — {q}" for q, sc in sorted(passed_through, key=lambda x: -x[1])] + [""]

    RESULTS.mkdir(parents=True, exist_ok=True)
    out = RESULTS / f"grounding_{datetime.now():%Y%m%d-%H%M}.md"
    out.write_text("\n".join(lines), encoding="utf-8")
    print(f"\nWrote {out}")
    return verdict


if __name__ == "__main__":
    raise SystemExit(main())
