# Local AI service — Sprint 2

The localhost service behind the digital human: speech in, grounded answer out, with
the audio and facial-animation timing the Unity avatar needs to speak it.

Runs on the VX PC alongside Unity. Binds `127.0.0.1` only. Unity is the sole client.

**The API contract lives in [`../docs/AI_SERVICE_CONTRACT.md`](../docs/AI_SERVICE_CONTRACT.md)** —
read that before writing anything on the Unity side.

## Quick start (macOS)

```bash
cd ~/AI-Hologram-and-Digital-Presence/service
bash scripts/mac_local_setup.sh
```

That does the whole of stage 1: installs and starts Ollama, pulls a chat model sized
to this machine's RAM plus the embedding model, builds the venv and the index,
calibrates the grounding floor, runs the tests, and smoke-tests the live service
against real Ollama. Safe to re-run. Everything it prints also lands in
`var/local-setup.log`.

Afterwards, `./run.sh` starts the service on its own.

### Talking to it

```bash
./run.sh                                  # one terminal
.venv/bin/python scripts/chat.py          # another
```

Type questions, see what the avatar would say plus what the service decided and why —
`answered`, `out of scope` or `refused`, with citations and latency. `/history` shows
what it currently remembers, `/reset` clears that, `/new` starts a conversation with
someone it has never met. This is the only thing that exercises multi-turn memory
interactively: the eval harness asks every question in isolation, so follow-ups like
"and what about that one?" only get tried here.

**Python 3.10–3.13, not 3.14.** pydantic-core has no 3.14 wheel and its source build
fails, and Homebrew installs `python@3.14` as an Ollama dependency — so `python3` on a
fresh Mac may point straight at the one version that cannot work. The setup script
picks a supported interpreter itself and rebuilds `.venv` if it finds the wrong one.
3.12 is the target: widest wheel coverage across all three stages.

### Stages

| Stage | Install | Gives you | Card |
|---|---|---|---|
| 1 | `requirements-core.txt` | Real retrieval, real grounded answers, speech mocked | T14, T15 |
| 2 | `requirements-stt.txt` | Real `/transcribe` | T13 |
| 3 | `bash scripts/stage3_speech.sh` | Real voice and blend shapes | T25 |

Stage 1 is deliberately small — no torch, because embeddings go through Ollama — so
the first working loop is minutes away rather than a multi-gigabyte download away.

No Ollama at all? `AIHOLO_MOCK=true` runs the whole contract without it — see
"Mock mode" in the contract document for what that does and does not prove.

## Layout

| Path | What it is |
|---|---|
| `app/main.py` | The routes. Start here. |
| `app/contracts.py` | The request/response models — these *are* the agreement with Unity. |
| `app/errors.py` | One typed error envelope for every failure. |
| `app/stt.py` | Speech to text behind an adapter, so the engine can be swapped after the benchmark. |
| `app/llm.py` | Ollama inference, structured output, grounding prompt. |
| `app/rag.py` | Chunking, indexing, retrieval, citations. Embeddings via Ollama. |
| `app/tts.py` | Azure Speech → wav + 55-shape 60 fps viseme track. |
| `app/sessions.py` | Multi-turn memory, in process, never written to disk. |
| `app/cli.py` | `index` and `check`. |
| `bench/` | The T06 and T07 benchmark harnesses, plus grounding-floor calibration. |
| `scripts/` | `mac_local_setup.sh` — one-command bring-up. `chat.py` — talk to it. |
| `corpus/` | The approved documents. Nothing else is sayable. |
| `tests/` | Contract tests — run in mock mode, no hardware needed. |
| `var/` | Generated audio, viseme tracks and the index. Gitignored. Set `AIHOLO_VAR_DIR` to divert it — do that for any experiment, or a mock-mode reindex will overwrite the index the running service is serving. |

## Secrets

The Azure key lives in `service/.env`, which is gitignored, and nowhere else. Unity
never holds a credential — it talks to this service, this service talks to Azure. If
a key ever lands in a commit, rotate it in the Azure portal rather than rewriting
history, because the old one is already public by then.

## Tests

```bash
cd service && python -m pytest -q
```

21 tests, none needing hardware, so they run on any laptop and can gate a PR before anyone
books lab time. They cover the contract shape, every typed error, viseme flattening,
grounded-vs-refused behaviour, session memory, and path traversal on the artefact
routes. They do **not** prove anything about real transcription accuracy, model
latency or Azure output — that is what the benchmarks and Dinesh's test matrix are for.

## Benchmarks

```bash
python -m bench.calibrate_floor        # sets the retrieval pre-filter
python -m bench.eval_grounding         # does it actually refuse? (service must be running)
python -m bench.eval_conversation      # multi-turn: follow-ups, pronouns, topic changes
python -m bench.verify_speech          # stage 3: audio + facial timeline vs the Unity contract
python -m bench.bench_ollama --models llama3.1:8b-instruct-q4_K_M qwen2.5:7b-instruct-q4_K_M
python -m bench.bench_stt    --configs small.en:int8 medium.en:int8
```

`eval_grounding` is the one that matters. The retrieval floor cannot tell you whether
the avatar refuses correctly — embedding similarity measures whether a question is
*about* the corpus, not whether the corpus *answers* it — so refusal is measured
through the whole pipeline instead. See "How grounding actually works" in the contract
document.

Both write a dated markdown report into `bench/results/` with the numbers each Planner
card asks for. `bench_stt` refuses to run unless the sample set actually has three
speakers and at least one noisy recording — the card's coverage rule is enforced in
code rather than left to memory.

Record the samples in the VX lab with the lab's own microphone and the Looking Glass
running, so its fan is in the noise floor. Naming: `<speaker>_<quiet|noisy>_<n>.wav`
with a matching `.txt` holding the reference transcript.


## Packaging for the VX PC

**Portable folder, not Docker.** Docker Desktop on Windows needs an admin install
plus WSL2, and GPU passthrough needs the NVIDIA Container Toolkit on top of that —
three things to ask VXLab's permission for, on a machine where installing software
is probably not permitted. The bundle instead is:

```
AI-Hologram-Service\
    ollama\          Ollama's standalone Windows zip - no installer
    models\          OLLAMA_MODELS points here; chat + nomic-embed-text
    python\          embeddable Python distribution
    app\ corpus\ var\
    start.bat        sets the env vars, starts Ollama, then the service
```

Nothing is installed, nothing needs admin, the GPU is reached directly rather than
through a container runtime, and the whole thing copies onto the machine as a folder.
This is also why embeddings go through Ollama rather than torch — one runtime to
bundle instead of two, and roughly 1.5 GB less to copy.

Revisit only if VXLab confirms Docker Desktop is already installed and usable.

## Troubleshooting

**Unity answers with the Sprint 1 recordings instead of the model.** The service is
not running. `scripts/stage3_speech.sh` and `scripts/mac_local_setup.sh` both stop it
when they finish, so start it again with `./run.sh` before pressing Play. The fallback
is deliberately silent — check the Unity console for `live service at …` and the
transcript log for `live` rather than `fallbk`, rather than assuming.

**"Python quit unexpectedly" / `zsh: segmentation fault ./run.sh`.** Diagnosed from
the crash report of 14 Sept 2026: a null dereference inside
`CSpxAppleCodecAdapter::SetFormat`, on a thread the Azure SDK spawned, with no Python
frames in the trace. Nothing in Python could catch it.

Synthesis therefore runs in a child process (`app/tts_worker.py`). A crash now costs
one answer: the parent sees a negative exit status, returns `TTS_UNAVAILABLE`, and
Unity's `FallbackAnswerSource` plays a recorded clip. `tests/test_speech_isolation.py`
proves the pattern by genuinely segfaulting a child and surviving it.

If the service itself ever dies again, the report is in
`~/Library/Logs/DiagnosticReports/Python-*.ips` — copy it into `service/var/` and read
the faulting thread's frames.

**Port 8765 already in use.** A service from an earlier run is still up:
`pkill -f "uvicorn app.main:app"`.
