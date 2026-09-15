"""Configuration. Every secret comes from service/.env, which is gitignored.

Nothing in this file may contain a key, and nothing in Unity may contain one either -
Unity talks to this service, this service talks to Azure.
"""
from __future__ import annotations

import os
from functools import lru_cache
from pathlib import Path

from pydantic_settings import BaseSettings, SettingsConfigDict

SERVICE_ROOT = Path(__file__).resolve().parent.parent

#: Generated state: the retrieval index, reply audio, viseme tracks. Overridable so
#: a test run or a throwaway experiment can point somewhere else instead of writing
#: over the index the service is actually serving - rebuilding it in mock mode by
#: accident takes the real one out and is confusing to diagnose from the other end.
#: The portable VX PC bundle also wants this somewhere other than beside the code.
#:
#: Read from the environment directly rather than from Settings because the paths
#: below are module-level constants that other modules import at import time.
VAR_DIR = Path(os.environ.get("AIHOLO_VAR_DIR") or (SERVICE_ROOT / "var"))
AUDIO_DIR = VAR_DIR / "audio"
VISEME_DIR = VAR_DIR / "visemes"
INDEX_DIR = VAR_DIR / "index"
CORPUS_DIR = SERVICE_ROOT / "corpus"


class Settings(BaseSettings):
    model_config = SettingsConfigDict(
        env_file=SERVICE_ROOT / ".env",
        env_prefix="AIHOLO_",
        extra="ignore",
    )

    host: str = "127.0.0.1"
    port: int = 8765

    ollama_url: str = "http://127.0.0.1:11434"
    ollama_model: str = "llama3.1:8b-instruct-q4_K_M"
    ollama_timeout_s: float = 25.0

    stt_model: str = "small.en"
    stt_device: str = "auto"
    stt_compute: str = "int8"

    #: An Ollama model, not a HuggingFace one - embeddings go through Ollama so
    #: torch stays out of the install. Pull it with: ollama pull nomic-embed-text
    embed_model: str = "nomic-embed-text"
    embed_timeout_s: float = 60.0
    retrieve_top_k: int = 4
    #: Corpus headings whose passages mean "this is not something I handle".
    #: Retrieving one is a REFUSAL that happens to be helpful, not an answer.
    out_of_scope_sections: str = "what i cannot help with,out of scope"
    grounding_floor: float = 0.28
    #: Mock mode matches on term coverage, not cosine, so it needs its own
    #: threshold. See rag.coverage() for why. grounding_floor above is calibrated
    #: for nomic-embed-text by scripts/calibrate_floor.py - rerun it if the embed
    #: model changes, and again on the VX PC against Anuji's 20-question set (T08).
    mock_coverage_floor: float = 0.5

    azure_key: str = ""
    azure_region: str = "australiaeast"
    azure_voice: str = "en-AU-NatashaNeural"
    #: Synthesis runs in a child process (see tts.py). Generous, because the cost of
    #: cutting a real synthesis short is a silent avatar.
    tts_timeout_s: float = 45.0

    mock: bool = False

    @property
    def out_of_scope_headings(self) -> tuple[str, ...]:
        return tuple(h.strip().lower() for h in self.out_of_scope_sections.split(",")
                     if h.strip())

    @property
    def azure_configured(self) -> bool:
        return bool(self.azure_key and self.azure_region)


@lru_cache
def settings() -> Settings:
    for d in (AUDIO_DIR, VISEME_DIR, INDEX_DIR, CORPUS_DIR):
        d.mkdir(parents=True, exist_ok=True)
    return Settings()
