"""Talk to the avatar from the terminal.

    ./run.sh                      # in one terminal
    .venv/bin/python scripts/chat.py     # in another

Type a question, get the answer the hologram would speak, plus what the service
decided and why. This is the fastest way to poke at the corpus, and the only thing
so far that exercises multi-turn memory interactively - the eval harness asks every
question in isolation, so follow-ups like "and what about that one?" have never
actually been tried.

Commands:
    /health     component readiness
    /new        start a fresh conversation (new session id, avatar forgets you)
    /reset      clear this conversation's memory, keep the session id
    /history    what the service currently remembers about this session
    /quit
"""
from __future__ import annotations

import sys
import uuid

import httpx

BASE = "http://127.0.0.1:8765"

# Outcome -> how it reads in the transcript. The distinction matters: "refused" and
# "not something I handle" are both correct, and telling them apart at a glance is
# the point of testing by hand.
BADGE = {
    "answered": "answered",
    "out_of_scope": "out of scope",
    "not_found": "refused",
}


def health(base: str) -> dict | None:
    try:
        return httpx.get(f"{base}/health", timeout=5).json()
    except Exception as exc:
        print(f"Cannot reach the service at {base}: {exc}")
        print("Start it first:  ./run.sh")
        return None


def show_health(h: dict) -> None:
    for part in ("stt", "model", "index", "azure"):
        row = h.get(part, {})
        print(f"  {'ok  ' if row.get('ready') else 'down'}  {part:6} {row.get('detail', '')}")
    if h.get("mock"):
        print("\n  MOCK MODE - answers are not coming from a model.")


def main() -> int:
    base = sys.argv[1] if len(sys.argv) > 1 else BASE
    h = health(base)
    if h is None:
        return 1

    print(f"Connected to {base}  (v{h.get('version')})")
    show_health(h)
    if not h.get("model", {}).get("ready"):
        print("\n  The model is not ready - answers will fail. Start Ollama.")
    if not h.get("azure", {}).get("ready"):
        print("\n  No speech yet: you get the text the avatar would say, not audio.")

    session = f"chat-{uuid.uuid4().hex[:8]}"
    print(f"\nSession {session}. Ask it something, or /quit.\n")

    while True:
        try:
            line = input("you  > ").strip()
        except (EOFError, KeyboardInterrupt):
            print()
            return 0
        if not line:
            continue

        if line in ("/quit", "/q", "/exit"):
            return 0
        if line == "/health":
            show_health(health(base) or {})
            continue
        if line == "/new":
            session = f"chat-{uuid.uuid4().hex[:8]}"
            print(f"  new session {session} - it has never met you before\n")
            continue
        if line == "/reset":
            httpx.post(f"{base}/session/{session}/reset", timeout=10)
            print("  conversation memory cleared\n")
            continue
        if line == "/history":
            # Over HTTP, not by importing the service's session store - importing it
            # here builds a SECOND store in this process, which is always empty and
            # makes working memory look broken.
            try:
                turns = httpx.get(f"{base}/session/{session}", timeout=10).json()["turns"]
            except Exception as exc:
                print(f"  could not read session: {exc}\n")
                continue
            print("  " + ("(nothing remembered yet)" if not turns else ""))
            for t in turns:
                print(f"  {t['role']:9} {t['text'][:100]}")
            print()
            continue

        try:
            r = httpx.post(f"{base}/answer",
                           json={"text": line, "sessionId": session, "speak": False},
                           timeout=180)
        except Exception as exc:
            print(f"  request failed: {exc}\n")
            continue

        try:
            d = r.json()
        except ValueError:
            print(f"  HTTP {r.status_code}, body was not JSON:\n  {r.text[:300]}\n")
            continue

        if "error" in d:
            e = d["error"]
            print(f"  {e['code']}: {e['message'][:200]}")
            print(f"  {'worth retrying' if e.get('retryable') else 'do not retry'}\n")
            continue

        outcome = d.get("outcome", "answered" if d.get("grounded") else "not_found")
        print(f"\nholo > {d['text']}\n")
        print(f"       [{BADGE.get(outcome, outcome)}]  {d['timings']['totalMs']} ms "
              f"(retrieve {d['timings']['retrieveMs']}, model {d['timings']['inferMs']})")
        for c in d.get("citations", []):
            where = c.get("section") or (f"page {c['page']}" if c.get("page") else "")
            print(f"       cited  {c['score']:.3f}  {c['title']} / {where}")
        if d.get("followUps"):
            print(f"       next   {' | '.join(d['followUps'])}")
        if d.get("speechUnavailable"):
            print(f"       audio  none ({d['speechUnavailable'][:60]})")
        print()


if __name__ == "__main__":
    raise SystemExit(main())
