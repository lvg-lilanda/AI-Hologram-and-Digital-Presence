# Sprint 2 AI service — what went wrong, and what now guards against it

Written 14 September 2026, after the first end-to-end bring-up on an Apple M3 Mac.
Every item below actually happened. Each one left a guard behind, and the guards are
the reason to read this before the VX lab session rather than after.

**The headline: almost none of these were bugs in the AI logic.** They were
environment, state and protocol problems. The VX PC is a different environment, so it
will have its own set — which is why the bring-up runbook front-loads environment
checks instead of leaving them to whatever fails first.

---

## A. Environment and toolchain

| # | What happened | Root cause | Guard now in place |
|---|---|---|---|
| A1 | `pip install` spent minutes compiling Rust, then failed | Homebrew installed `python@3.14` **as an Ollama dependency**, so `python3` pointed at it. pydantic-core has no 3.14 wheel and PyO3 caps at 3.13. | Setup picks a supported interpreter itself (prefers 3.12), and rebuilds a venv built on the wrong one. `run.sh` and `run.ps1` both refuse a bad venv up front. |
| A2 | 126 MB of Linux binaries appeared in the repo | A venv was created inside the mounted folder from a Linux sandbox — wrong architecture for the Mac that owns the folder. | Never create venvs from the sandbox side. `.venv/` is gitignored. |
| A3 | `ModuleNotFoundError` far from its cause | A broken venv was silently reused. | Version guard in both start scripts, with the fix in the message. |
| A4 | One root cause reported as four separate failures | The setup script kept going after the dependency install failed. | Fail-fast: it stops at the first fatal step and says so. |
| A5 | `(some components are down)` on every single run | `faster-whisper` is stage 2 and not installed yet — reported as `DOWN` alongside real failures. | Pending stages show `--`; only a genuine failure prints the warning, so the warning means something. |

## B. State and data integrity

| # | What happened | Root cause | Guard now in place |
|---|---|---|---|
| B1 | *(near miss)* An index built in mock mode would load silently into a real run | Two embedding models produce incompatible vector spaces. Retrieval would return nonsense with no error anywhere. | The index records the embedder and a schema version; a mismatch is refused loudly with the rebuild command. **This later caught a real occurrence.** |
| B2 | A live index was overwritten with a mock one | A test reindex was run inside the live folder. Same disk, same `var/`. | `AIHOLO_VAR_DIR` diverts all generated state; experiments point elsewhere. |
| B3 | The test suite rebuilt the shared index mid-script | The fixture indexed into `var/index` in mock mode, between the real index build and the live smoke test. | The fixture indexes into a temp directory and restores the paths afterwards. |

## C. Grounding correctness

| # | What happened | Root cause | Guard now in place |
|---|---|---|---|
| C1 | No cosine threshold could separate answerable from unanswerable | Measured: *"What is Telstra's share price today?"* scored **0.717**; *"Can I ask you anything I like?"* scored **0.511**. Embedding similarity measures whether a question is *about* the corpus, not whether the corpus *answers* it. | The floor was demoted to a cheap pre-filter. The corpus gained an explicit "what I cannot help with" section. The model makes the real decision. |
| C2 | Five correct refusals scored as hallucinations | The model was asked to emit a `NOT_IN_CONTEXT` sentinel; llama3.1 refuses by *paraphrasing* instead. | The model returns a structured `status` field. Wording is irrelevant. |
| C3 | Good out-of-scope refusals counted as inventions | `grounded` was doing two jobs. | Three outcomes: `answered` / `out_of_scope` / `not_found`. |
| C4 | **A real hallucination**: muru-D "focuses on supporting startups and entrepreneurs" | Asked a vague follow-up, retrieval got almost no signal, and the model filled the gap from training. The words appear nowhere in the corpus. | The model must name the passages it used; an answer citing none is downgraded to `not_found`. Retrieval now sees the previous question. Citations list only what was used. |
| C5 | The class of bug above was invisible to the tests | Every eval question was asked cold. Nobody talks to a hologram that way. | `bench/eval_conversation.py` runs multi-turn scripts. C4's exact conversation is the first regression case. |

## D. Service robustness

| # | What happened | Root cause | Guard now in place |
|---|---|---|---|
| D1 | `/answer` returned an unreadable non-JSON 500 | The Azure SDK was imported before the "is Azure configured?" check, so a missing optional dependency escaped as a bare `ImportError`. | Config checked first, import guarded. Speech that is *not set up yet* returns the text answer with `speechUnavailable`; speech that is *broken* returns `TTS_UNAVAILABLE`. |
| D2 | **The service died mid-run**: `zsh: segmentation fault` | A null dereference inside `CSpxAppleCodecAdapter::SetFormat`, on a thread the Azure SDK spawned. No Python frames in the crash report. Nothing in Python could catch it. | Synthesis runs in a child process. A crash costs one answer; the parent returns `TTS_UNAVAILABLE` and Unity plays a recorded clip. |
| D3 | `Speech worker produced unreadable output:` with an empty stderr | The worker returned its result on **stdout**, which the Azure SDK's native library also writes to. SDK noise was prefixed to the JSON. | The result comes back through a file. stdout and stderr belong to the SDK, captured as diagnostics and attached to any error. |
| D4 | Three rounds to diagnose D2 | 503s were logged with a status code and no cause. | Every typed error logs its code and message server-side; unhandled exceptions return a typed envelope instead of plain text. |

## E. Unity integration

| # | What happened | Root cause | Guard now in place |
|---|---|---|---|
| E1 | Answers with no audio vanished silently | `AvatarController` returned before raising `Spoke`, so only a console warning appeared. | A text-only answer is captioned and follow-ups still advance. The avatar degrades visibly instead of appearing broken. |
| E2 | *(prevented)* Sending menu ids to the service | `MenuQuestionSource` raises entry ids (`murud`); the service does retrieval over natural language and `murud` retrieves almost nothing — it would have answered *worse* with no error. | `FallbackAnswerSource` translates via `CannedAnswerSource.PromptFor`. |
| E3 | The avatar used recorded answers and looked like a regression | The service was not running, and the `Use Live Service` tick was never saved into the scene. | Console logs `live service at …`; the transcript log writes `live` vs `fallbk`. Check those, never the caption. |

## F. Test hygiene

| # | What happened | Root cause | Guard now in place |
|---|---|---|---|
| F1 | 30 tests errored at once | Two test modules mutated `os.environ` at import time, leaking across the whole pytest session. | Tests build their `Settings` explicitly. The suite passes with and without the env var set. |

---

## What this tells us for the VX lab

**The code is now the hardened part; the environment is where the risk lives.** Of
the nineteen items above, twelve were environment, state, protocol or process — only
C1–C5 touched the AI logic itself, and those are now measured rather than assumed.

Three items transfer directly to Windows and are worth pre-empting:

- **A1** — the Python version trap is not macOS-specific. Anything that puts a newer
  Python on PATH reproduces it. Pin 3.12 deliberately.
- **B1/B2** — the index must be rebuilt on the VX PC with the same embedding model the
  service will run. Copying `var/` across machines is the wrong move; build it there.
- **D2** — the segfault was in an *Apple* codec path and will not occur on Windows.
  The subprocess isolation stays anyway: it protects against whatever the Windows
  equivalent turns out to be, at a cost of one interpreter start per answer.

And one item is the reason a bad lab session cannot become a lost lab session:
**E1 plus D2 mean the avatar keeps talking when the AI stops.** That degradation path
was exercised unrehearsed on 14 September — the service died and the demo continued
on recorded clips.
