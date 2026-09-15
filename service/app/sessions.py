"""Multi-turn memory, keyed by the session id Unity sends.

Sprint 2 Week 3 builds memory rather than voice input, so the store is deliberately
small: the last few turns per session, in memory, cleared on restart. Nothing here
is persisted - a lab demo has no reason to keep a visitor's conversation on disk,
and not writing it is the simplest privacy answer to give the client.
"""
from __future__ import annotations

import time
from collections import deque
from dataclasses import dataclass, field
from threading import Lock

MAX_TURNS = 6          # 3 exchanges - enough for "and what about that one?"
SESSION_TTL_S = 30 * 60


@dataclass
class Turn:
    role: str          # "user" | "assistant"
    text: str


@dataclass
class Session:
    turns: deque[Turn] = field(default_factory=lambda: deque(maxlen=MAX_TURNS))
    touched: float = field(default_factory=time.time)


class SessionStore:
    def __init__(self):
        self._sessions: dict[str, Session] = {}
        self._lock = Lock()

    def history(self, session_id: str) -> list[Turn]:
        with self._lock:
            self._evict()
            s = self._sessions.get(session_id)
            return list(s.turns) if s else []

    def record(self, session_id: str, user_text: str, assistant_text: str) -> None:
        with self._lock:
            s = self._sessions.setdefault(session_id, Session())
            s.turns.append(Turn("user", user_text))
            s.turns.append(Turn("assistant", assistant_text))
            s.touched = time.time()

    def reset(self, session_id: str) -> None:
        with self._lock:
            self._sessions.pop(session_id, None)

    def count(self) -> int:
        with self._lock:
            self._evict()
            return len(self._sessions)

    def _evict(self) -> None:
        cutoff = time.time() - SESSION_TTL_S
        for key in [k for k, v in self._sessions.items() if v.touched < cutoff]:
            del self._sessions[key]
