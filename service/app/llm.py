"""Ollama inference and answer orchestration.

T14 acceptance criteria:
  - "The selected Ollama model returns a structured answer."
        format=json plus a schema check, so the reply is always {answer, followUps}
        and never a paragraph the UI has to guess at.
  - "Timeout, unavailable model and invalid output are handled."
        MODEL_TIMEOUT / MODEL_UNAVAILABLE / INVALID_MODEL_OUTPUT, all typed.

Grounding rule: the model classifies its own reply as answered / out_of_scope /
not_found AND names which passages it used. main.py checks both - the scope boundary
must come from the corpus, and an "answered" reply that cannot point at a real
retrieved passage is downgraded to not_found.

That second check exists because of a measured failure. Asked "ok tell me about the
project" after two earlier turns, llama3.1 answered that muru-D "focuses on supporting
startups and entrepreneurs, providing them with resources and expertise" - fluent,
confident, cited, and nowhere in the corpus. It is what the model knows about the real
muru-D accelerator from training, filling a gap left by weak retrieval. "Use only the
passages" in a prompt does not prevent that; having to name the passage does.
"""
from __future__ import annotations

import json
import time

import httpx

from .config import Settings
from .errors import ErrorCode, ServiceError
from .rag import Chunk
from .sessions import Turn

#: The three outcomes the model classifies its own reply into. Asking for a field is
#: far more reliable than asking it to emit a magic string inside the answer text:
#: measured on llama3.1:8b, refusals came back paraphrased ("I don't have anything on
#: that in the material I've been given") rather than as the sentinel, so a
#: string-match classifier scored correct refusals as inventions.
STATUSES = ("answered", "out_of_scope", "not_found")

SYSTEM_PROMPT = """You are a digital human presenting at Telstra muru-D, speaking aloud \
to a person standing in front of a holographic display.

Use ONLY the numbered passages provided. Never use outside knowledge, even when you \
know the answer.

First decide which of these three is true, and put it in "status":

  "answered"     - the passages contain what the question asks for.
  "out_of_scope" - the passages state that this topic is something you do not handle
                   (for example customer service, financial or commercial questions,
                   purchasing, bookings, or general knowledge).
  "not_found"    - the passages simply do not cover the question.

Then write "answer":

  For "answered"     - answer the question from the passages.
  For "out_of_scope" - say plainly that it is not something you can help with, and
                       name one thing you CAN talk about.
  For "not_found"    - say you do not have anything on that.

Style for every answer: two or three spoken sentences. No markdown, no lists, no \
emoji, no stage directions. Write numbers and abbreviations the way they should be \
said out loud.

"usedPassages": the numbers of the passages your answer actually came from, for \
example [1, 3]. Every fact in your answer must be in one of them. If you cannot point \
to a passage that supports what you wrote, your status is "not_found" - do not fill \
the gap from your own knowledge, even about well-known organisations.

"followUps": two short questions the person could ask next, drawn from the passages. \
Use an empty list when status is not "answered".

Reply with JSON only:
{"status": "answered|out_of_scope|not_found", "answer": "...", "usedPassages": [1], \
"followUps": ["...", "..."]}"""


def _build_prompt(question: str, passages: list[Chunk], history: list[Turn]) -> str:
    blocks = []
    for i, c in enumerate(passages, 1):
        where = c.section or (f"page {c.page}" if c.page else "")
        blocks.append(f"[{i}] {c.title}{' - ' + where if where else ''}\n{c.text}")
    parts = ["PASSAGES:\n" + ("\n\n".join(blocks) if blocks else "(none)")]
    if history:
        recent = "\n".join(f"{t.role}: {t.text}" for t in history)
        parts.append("CONVERSATION SO FAR:\n" + recent)
    parts.append(f"QUESTION: {question}")
    return "\n\n".join(parts)


class OllamaClient:
    def __init__(self, cfg: Settings):
        self._cfg = cfg

    def ready(self) -> tuple[bool, str]:
        try:
            r = httpx.get(f"{self._cfg.ollama_url}/api/tags", timeout=3.0)
            r.raise_for_status()
            names = [m.get("name", "") for m in r.json().get("models", [])]
        except Exception as exc:
            return False, f"Ollama unreachable at {self._cfg.ollama_url}: {exc}"
        if self._cfg.ollama_model not in names:
            return False, (f"model '{self._cfg.ollama_model}' not pulled "
                           f"(available: {', '.join(names) or 'none'})")
        return True, self._cfg.ollama_model

    def generate(self, question: str, passages: list[Chunk],
                 history: list[Turn]) -> tuple[str, str, list[str], list[int], int]:
        """Return (status, answer, follow-ups, used passage numbers, inference ms)."""
        payload = {
            "model": self._cfg.ollama_model,
            "system": SYSTEM_PROMPT,
            "prompt": _build_prompt(question, passages, history),
            "format": "json",
            "stream": False,
            "options": {"temperature": 0.2, "num_predict": 220},
        }
        started = time.perf_counter()
        try:
            r = httpx.post(f"{self._cfg.ollama_url}/api/generate", json=payload,
                           timeout=self._cfg.ollama_timeout_s)
        except httpx.TimeoutException as exc:
            raise ServiceError(
                ErrorCode.MODEL_TIMEOUT,
                f"Model did not respond within {self._cfg.ollama_timeout_s}s.",
            ) from exc
        except httpx.HTTPError as exc:
            raise ServiceError(
                ErrorCode.MODEL_UNAVAILABLE, f"Cannot reach Ollama: {exc}"
            ) from exc
        infer_ms = int((time.perf_counter() - started) * 1000)

        if r.status_code == 404:
            raise ServiceError(
                ErrorCode.MODEL_UNAVAILABLE,
                f"Ollama has no model named '{self._cfg.ollama_model}'. Pull it first.",
            )
        if r.status_code >= 400:
            raise ServiceError(
                ErrorCode.MODEL_UNAVAILABLE, f"Ollama returned {r.status_code}: {r.text[:200]}"
            )

        raw = r.json().get("response", "")
        try:
            parsed = json.loads(raw)
            answer = str(parsed["answer"]).strip()
            status = str(parsed.get("status", "")).strip().lower()
            follow_ups = [str(f).strip() for f in parsed.get("followUps", [])][:2]
            used = [int(n) for n in parsed.get("usedPassages", [])
                    if str(n).strip().lstrip("-").isdigit()]
        except (json.JSONDecodeError, KeyError, TypeError) as exc:
            raise ServiceError(
                ErrorCode.INVALID_MODEL_OUTPUT,
                f"Model reply was not the agreed JSON shape: {exc}",
                received=raw[:200],
            ) from exc

        if not answer:
            raise ServiceError(ErrorCode.INVALID_MODEL_OUTPUT, "Model returned an empty answer.")
        if status not in STATUSES:
            # An unrecognised status is treated as "no idea", never as "answered" -
            # the safe direction when the demo is in front of the client.
            status = "not_found"
        return status, answer, follow_ups, used, infer_ms


class MockLlm:
    """Answers from the passages without a model, so Unity work is never blocked
    on the VX PC being free. Never use this in front of the client."""

    def __init__(self, cfg: Settings):
        self._cfg = cfg

    def ready(self) -> tuple[bool, str]:
        return True, "mock - no model loaded"

    def generate(self, question: str, passages: list[Chunk],
                 history: list[Turn]) -> tuple[str, str, list[str], list[int], int]:
        if not passages:
            return "not_found", "I don't have anything on that.", [], [], 1
        first = passages[0]
        if first.out_of_scope:
            return ("out_of_scope", first.text.strip().replace("\n", " ")[:280], [], [1], 1)
        return ("answered", first.text.strip().replace("\n", " ")[:280],
                ["Tell me more about that", "What else can you do?"], [1], 1)


def build_llm(cfg: Settings):
    return MockLlm(cfg) if cfg.mock else OllamaClient(cfg)
