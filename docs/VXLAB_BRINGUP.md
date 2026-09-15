# Bring-up runbook — clean folder, then the VX PC

Two parts. Part 1 gets the AI into a clean checkout on a Mac. Part 2 is the VX lab
session, ordered so the things that could stop you are settled first.

Read [SPRINT2_AI_POSTMORTEM.md](SPRINT2_AI_POSTMORTEM.md) first if you want to know
why each check exists. Every one of them is there because something went wrong.

---

## Part 1 — Getting the AI into a clean folder

**Move it with git, not by copying files.** The AI is 46 files and about 4,900 lines,
but sitting beside them in `service/` is 387 MB of generated state — `var/` (112 MB)
and `.venv` (275 MB) — that must not travel. Both are gitignored, so a commit carries
exactly the right things and nothing else. It also puts the work in the repository,
which is one of the four artifacts the weekly submission is assessed on.

```bash
# In the current folder - everything is already staged
cd ~/AI-Hologram-and-Digital-Presence
git status --short                     # confirm nothing unexpected
git commit -m "feat(service): add local AI service, Unity live answer source and evaluation harnesses"
git push -u origin feature/sprint2-ai-service
```

Two things to decide before pushing:

- The branch was cut from `feature/week3-demo-script`, not `main`, so it carries that
  unmerged work. Rebase onto `main` or say so in the PR.
- `Assets/Scenes/Team11 - 3D Model Test.unity` is modified but **unstaged** — that is
  your own scene work, not the AI. Stage it separately if you want it.

Then in the clean checkout:

```bash
git checkout feature/sprint2-ai-service
cd service
cp .env.example .env          # then paste the Azure key and region back in
bash scripts/mac_local_setup.sh
bash scripts/stage3_speech.sh
```

`.env` is the one thing git will not bring across, by design. Keep the key somewhere
you can paste it from; it is never in the repository.

### Before you call the clean folder ready

| Check | Command | Expected |
|---|---|---|
| Tests | `.venv/bin/python -m pytest -q` | 43 passed |
| Components | `.venv/bin/python -m app.cli check` | `--` for stt, OK for model / index / azure |
| Refusals | `python -m bench.eval_grounding` | 16/16 |
| Conversations | `python -m bench.eval_conversation` | every turn behaved |
| Speech contract | `python -m bench.verify_speech` | 12/12 |
| Unity | tick **Use Live Service**, press Play, **then Cmd+S** | console says `live service at …` |

That last step is the one that has already bitten once: the tick is scene state, and
closing Unity without saving discards it silently.

---

## Part 2 — The VX lab session

### Work in this order

The lab is time-boxed and the unknowns are not equally risky. Settle whether the
machine can run this **at all** before spending an hour on Python.

**1. Can Ollama run here?** (15 min — everything else depends on it)

Portable, no admin: download the Ollama Windows zip, extract to the project folder,
set `OLLAMA_MODELS` to a folder you can write to, run `ollama serve`. If it will not
run and cannot be installed, stop — the rest of the day is Unity plus recorded clips,
and that is still a working demo.

**2. Is there a GPU, and how slow is it without one?** (10 min)

```
nvidia-smi
ollama run llama3.1:8b-instruct-q4_K_M "say hello in five words"
```

On an M3 Mac an answer took **5–11 seconds**. On a CPU-only machine an 8B model can
take 30 s or more, which is not demonstrable. If it is slow, pull
`llama3.2:3b-instruct-q4_K_M` and re-run `bench/bench_ollama.py` to compare — that is
what the benchmark card is for, and the smaller model is a legitimate answer.

**3. Can it reach Azure?** (5 min)

```
curl -I https://australiaeast.tts.speech.microsoft.com/
```

Lab networks block outbound traffic more often than not. If this fails there is no
speech: the avatar captions its answers and uses recorded clips for audio. Decide
whether that is acceptable for the client checkpoint **before** you are standing in
front of it, not during.

**4. Python 3.12** (15 min)

Install python.org 3.12 or use an embeddable 3.12 in the project folder. **Do not use
3.13+ or whatever is already on PATH** — pydantic-core has no wheel beyond 3.13 and
the source build fails after several minutes of Rust compilation. `run.ps1` checks
this and refuses rather than letting you find out the slow way.

```
python -m venv .venv
.venv\Scripts\pip install -r requirements.txt
```

**5. Configure and index** (10 min)

```
copy .env.example .env          # paste key and region; set AIHOLO_OLLAMA_MODEL
.venv\Scripts\python -m app.cli index
.venv\Scripts\python -m app.cli check
```

Build the index **on the VX PC**. Do not copy `var/` from the Mac: the index records
which embedder built it and the service refuses a mismatch, which is the correct
behaviour but a confusing way to spend lab time.

**6. Verify before touching Unity** (10 min)

```
.\run.ps1
.venv\Scripts\python -m bench.eval_grounding
.venv\Scripts\python -m bench.verify_speech
.venv\Scripts\python -m bench.calibrate_floor
```

`calibrate_floor` is worth rerunning here: scores shift with the machine and the
model, and the recommended floor may differ from the Mac's 0.54.

**7. Unity, last** (remaining time)

Open the scene, tick **Use Live Service**, Play. Assign the `Thinking` clip first —
with a 5–11 second answer, silence reads as a freeze and it is the single cheapest
thing you can do for how the demo feels.

### If it goes wrong, you still have a demo

There are three levels and you cannot come away with nothing:

| Level | Needs | What the client sees |
|---|---|---|
| 0 | Unity only | The avatar answers five scripted questions with recorded speech and full lip sync. This is the Sprint 1 demo and it has no dependencies. |
| 1 | + Ollama + index | Real answers to real questions, captioned. No voice. |
| 2 | + Azure reachable | Real answers, real voice, real facial animation. |

Level 0 is the floor, and it is automatic: `FallbackAnswerSource` drops to recorded
clips whenever the service cannot answer. That path was exercised unrehearsed on
14 September when the service crashed mid-session — the avatar kept talking.

### Things that will look like failures and are not

- **`-- stt NOT_INSTALLED`** — stage 2, not needed for this demo.
- **The same five buttons** — free text needs the microphone (T19). What changed is
  where the answer comes from, not how it is asked.
- **A recorded-sounding answer** — check the console for `live service at …` and the
  transcript for `live` vs `fallbk`. The caption alone tells you nothing.
- **`TTS_UNAVAILABLE … killed by signal`** — the Azure SDK crashed; the service is
  fine and the avatar falls back. Worth noting, not worth stopping for.

### What to capture while you are there

The lab session is evidence for four cards, and the harnesses write it for you:

- `bench/results/grounding_eval_*.md` — T15
- `bench/results/conversation_eval_*.md` — T15, multi-turn
- `bench/results/speech_verify_*.md` — T25
- `bench/results/ollama_*.md` — T06, with the GPU question answered
- End-to-end timings from the eval output — T27's baseline on the real hardware

Record them as **verified on the VX PC**, which is the distinction that has been
missing from every claim so far.
