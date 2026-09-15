# Benchmark protocol — VX lab

Bench-side procedure. Run in order; each step feeds the next. Every harness writes a
dated report into `service/bench/results/`, which is the evidence for the cards.

**Mac baseline for comparison (14 Sept 2026, M3 16 GB, no GPU):** grounding 16/16,
speech contract 12/12, answers 4.9–11.3 s, cold start 10.9 s.

Record the VX PC's numbers beside those. The comparison is the finding.

---

## 0. Start the service (5 min)

```
cd <repo>\service
.\run.ps1
```

Leave it running in its own window. Expect:

```
--    stt    NOT_INSTALLED: faster-whisper is stage 2
OK    model  llama3.1:8b-instruct-q4_K_M
OK    index  11 chunks from 1 documents via nomic-embed-text
OK    azure  en-AU-NatashaNeural @ australiaeast
```

`--` is fine — it means a later bring-up stage. Only `DOWN` is a problem.

**If `index` is DOWN:** `python -m app.cli index` — the index must be built on this
machine, not copied from the Mac.

**If `azure` is DOWN:** you have no speech. Steps 1–3 still work; skip 4 and 5 and
note it. The demo degrades to captions.

Then warm the model so the cold start does not pollute step 1:

```
curl -s -X POST http://127.0.0.1:8765/answer -H "Content-Type: application/json" -d "{\"text\":\"What is Telstra muru-D?\",\"sessionId\":\"warmup\",\"speak\":false}"
```

---

## 1. Grounding — does it refuse correctly? (2 min) · T15

```
python -m bench.eval_grounding
```

**Expect 16/16.** The only box that must be zero is *unanswerable → answered*.

Anything in that box is real: the avatar invented something. Do not raise the floor —
add an out-of-scope line to `service/corpus/project-overview.md`, re-index, re-run.

## 2. Multi-turn conversations (3 min) · T15

```
python -m bench.eval_conversation
```

Four scripted conversations: pronoun follow-ups, topic changes, and the exact
exchange where it once hallucinated. **Expect every turn to behave.**

This is the step most likely to surprise you. A failure here is worth more than a
pass — write down the turn and what it said.

## 3. Latency (3 min) · T27

```
python -m bench.bench_latency --turns 12 --note "GPU: <yes/no>, Unity: <running? fps>"
```

Reports retrieval / inference / speech separately, median and p90, cold start apart.

**Open Unity's Stats panel and note the frame rate while this runs** — the card asks
for frame rate and service resource use recorded together, and only you can see it.

To separate model cost from Azure cost:

```
python -m bench.bench_latency --turns 12 --no-speech
```

**If median is above ~5 s**, the `Thinking` clip on `AvatarController` stops being
optional. If it is above ~15 s, switch to the 3B model (step 6) before the demo.

## 4. Speech contract (1 min) · T25

```
python -m bench.verify_speech
```

**Expect 12/12.** Two checks matter most: the facial timeline matching the audio
length, and the blend shapes actually moving. A track that passes structurally but
sits at zero gives you an avatar reciting with its mouth shut.

## 5. Audio listening (10 min, headphones) · T25

Follow `service/bench/LISTENING_CHECK.md`. Generate clips with
`python scripts/chat.py`, play them from `service/var/audio/`.

**Check "muru-D" first.** A generic neural voice usually says "murr-ud" rather than
"moo-roo-dee", and it is in almost every answer. Also RMIT (letters, not "rem-it").

Test through the speakers the demo will use, standing where a visitor stands, with
the Looking Glass fan running. Your desk is not the room.

## 6. Model comparison (10 min, 2 GB pull) · T06

```
ollama pull llama3.2:3b-instruct-q4_K_M
python -m bench.bench_ollama --models llama3.1:8b-instruct-q4_K_M llama3.2:3b-instruct-q4_K_M --note "Unity running on the Looking Glass throughout"
```

Requires two models; the script enforces it. Records first-token time, total time,
RAM and VRAM, and writes every answer out for you to score by hand.

**Do this before step 3 if the machine feels slow** — benchmarking latency on a model
you are about to replace is wasted time. The 3B is a legitimate answer on a CPU-only
machine, not a compromise.

## 7. Speech-to-text (30 min, whole team) · T07

Only if there is time and people. Needs three speakers and a noisy recording; the
script refuses to run without them.

```
pip install -r requirements-stt.txt
```

Record in the lab with the actual microphone, Looking Glass running so its fan is in
the noise floor. Name files `<speaker>_<quiet|noisy>_<n>.wav` (16 kHz mono 16-bit)
with a matching `.txt` holding what was said, in `service/bench/samples/`.

```
python -m bench.bench_stt --configs small.en:int8 medium.en:int8
```

---

## Record as you go

| Step | Card | Result | Report file |
|---|---|---|---|
| 1 Grounding | T15 | ___ / 16 | `grounding_eval_*.md` |
| 2 Conversations | T15 | ___ turns behaved | `conversation_eval_*.md` |
| 3 Latency | T27 | median ___ ms, cold ___ ms | `latency_*.md` |
| 4 Speech contract | T25 | ___ / 12 | `speech_verify_*.md` |
| 5 Listening | T25 | pronunciation ok? | notes |
| 6 Models | T06 | chosen: ___ | `ollama_*.md` |
| 7 STT | T07 | chosen: ___ | `stt_*.md` |

Also note, because none of the scripts can see them:

- GPU present? Model? VRAM?
- Unity frame rate while the service is answering
- Whether Azure was reachable from the lab network
- Anything that needed admin rights

## Commit the evidence before you leave

`bench/results/` is gitignored — deliberately, because the STT samples are large and
speaker-identifiable. Add the reports by hand:

```
git add -f service/bench/results/*.md
git commit -m "test: VX lab benchmark evidence"
git push
```

Without `-f` they will be silently ignored and the lab session leaves no trace.

## If it all goes wrong

Level 0 still works: Unity with the recorded clips, no Ollama, no Azure, no network.
`FallbackAnswerSource` does that automatically. You cannot come away with nothing —
so spend the time on measurement, not on rescuing a broken install.
