"""Where a generated reply's audio and viseme track live until Unity has fetched them.

Files, not memory: Unity pulls the clip with UnityWebRequestMultimedia.GetAudioClip,
which wants a URL, and a file on disk survives the one retry Unity might make. The
folder is pruned so a long demo session cannot fill the VX PC's disk.
"""
from __future__ import annotations

import time
from pathlib import Path

from .config import AUDIO_DIR, VISEME_DIR
from .contracts import VisemeTrackModel

KEEP_FILES = 200


def _prune(folder: Path, suffix: str) -> None:
    files = sorted(folder.glob(f"*{suffix}"), key=lambda p: p.stat().st_mtime)
    for stale in files[:-KEEP_FILES]:
        try:
            stale.unlink()
        except OSError:
            pass


def save(answer_id: str, wav_bytes: bytes, track: VisemeTrackModel) -> tuple[str, str]:
    """Write both artefacts and return the URLs that go into AnswerResponse."""
    (AUDIO_DIR / f"{answer_id}.wav").write_bytes(wav_bytes)
    (VISEME_DIR / f"{answer_id}.json").write_text(
        track.model_dump_json(), encoding="utf-8"
    )
    _prune(AUDIO_DIR, ".wav")
    _prune(VISEME_DIR, ".json")
    return f"/audio/{answer_id}.wav", f"/visemes/{answer_id}.json"


def new_id() -> str:
    return f"a{int(time.time() * 1000):x}"
