# Sprint 2 AI service — the Unity contract

The agreement between the localhost AI service (`service/`, owned by Ali) and the
Unity client (owned by Lilan). Anything not in this document is an implementation
detail either side may change; anything in it needs both owners to agree first.

Service version: `0.1.0-sprint2`. Base URL on the VX PC: `http://127.0.0.1:8765`.

---

## 1. Where this sits

```
User speaks
   ↓  Unity records microphone audio            (Lilan, T19)
   ↓  POST /transcribe        localhost HTTP
Local speech-to-text turns it into text          (Ali, T13)
   ↓  POST /answer
RAG finds relevant passages in approved documents (Ali, T15)
   ↓
Ollama writes a grounded reply                    (Ali, T14)
   ↓
Azure Speech returns audio + viseme timing        (Ali, T25)
   ↓  GET /audio/… and GET /visemes/…
Unity plays it through the avatar                 (Lilan, T22/T26/T27)
```

Everything except Azure runs on the VX PC. Unity never holds a credential.

## 1b. Running it in Unity

1. Start the service: `cd service && ./run.sh`
2. Click into the Unity Editor so it recompiles.
3. Open `Assets/Scenes/Team11 - 3D Model Test.unity`, select the **AI** GameObject.
4. On `AvatarController`, tick **Use Live Service**. Leave the URL at
   `http://127.0.0.1:8765`.
5. Press Play.

The console logs `[AvatarController] live service at … session …` when the live path
is active, and the transcript log writes `live` rather than `canned` per answer — that
is how you tell it is really talking to the model and not quietly falling back.

What to expect, so "working" is recognisable:

- **The same five buttons.** Free-text questions need the microphone (T19); the menu
  still raises the Sprint 1 entry ids. What changed is where the *answer* comes from.
- **A 5–11 second pause** before the avatar responds. Assign the `Thinking` clip on
  `AvatarController` or it reads as frozen.
- **No mouth movement until stage 3.** With no Azure key the service returns a null
  `audioUrl`, so the answer arrives as a caption with no audio and no viseme track.
- **Falls back silently by design.** If the service is down, answers come off disk and
  the transcript says `fallbk`. Check the log rather than assuming the live path ran.

A detail that is easy to lose: `MenuQuestionSource` raises entry **ids** (`murud`),
because that is what `CannedAnswerSource` looks up — but the service does retrieval
over natural language, and `murud` retrieves almost nothing. `FallbackAnswerSource`
translates via `CannedAnswerSource.PromptFor` before calling the service. Send the id
and it answers worse, with no error anywhere.

## 2. Seam

`AvatarController` already holds an `IAnswerSource`, not a concrete class
(`Assets/Team 11/AI_Questions/IAnswerSource.cs`). Sprint 2 adds:

- `LiveAnswerSource` — calls this service.
- `FallbackAnswerSource` — tries `LiveAnswerSource`, drops to `CannedAnswerSource`
  on any failure or timeout, and sets `Answer.IsFallback = true`.

`VisemePlayer`, `AvatarController`, `OptionMenuUI` and the transcript log do not
change. That was the point of the Sprint 1 seam.

## 3. Endpoints

### `GET /health`

```json
{ "ok": true, "mock": false, "version": "0.1.0-sprint2",
  "stt":   { "ready": true,  "detail": "small.en (int8)" },
  "model": { "ready": true,  "detail": "llama3.1:8b-instruct-q4_K_M" },
  "index": { "ready": true,  "detail": "142 chunks from 6 documents" },
  "azure": { "ready": false, "detail": "AIHOLO_AZURE_KEY not set in service/.env" } }
```

Each component reports separately so a red line names the thing to fix. Unity should
call this once at start-up and show the operator a clear "AI offline" state rather
than failing on the first question in front of the client.

### `POST /transcribe`

`multipart/form-data`, field name `audio`.

**Audio format — 16 kHz, mono, 16-bit signed PCM, RIFF WAV.** Anything else is
rejected as `INVALID_AUDIO` rather than resampled. A silent resample is how a demo
ends up with lag nobody can explain, so the mismatch is made loud instead.

```json
{ "text": "What is Telstra muru-D?", "durationMs": 2140,
  "processingMs": 310, "engine": "faster-whisper:small.en", "language": "en" }
```

### `POST /answer`

```json
{ "text": "What is Telstra muru-D?", "sessionId": "<stable per visitor>", "speak": true }
```

`sessionId` is what makes multi-turn memory work — keep it stable for a conversation
and generate a new one when the operator resets between visitors. The service keeps
the last three exchanges in memory only, and never writes them to disk.

```json
{ "id": "a1a09b54eec5", "sessionId": "demo-1",
  "text": "Telstra is Australia's largest telecommunications company…",
  "followUps": ["…", "…"],
  "citations": [{ "title": "AI Hologram and Digital Presence",
                  "section": "What Telstra muru-D is", "page": null, "score": 0.38 }],
  "outcome": "answered", "grounded": true, "isFallback": false,
  "audioUrl": "/audio/a1a09b54eec5.wav",
  "visemesUrl": "/visemes/a1a09b54eec5.json",
  "timings": { "retrieveMs": 12, "inferMs": 1840, "ttsMs": 420, "totalMs": 2290 } }
```

`outcome` has three values, and the middle one is the reason it exists:

| `outcome` | `grounded` | Meaning |
|---|---|---|
| `answered` | `true` | The corpus covers the question. |
| `out_of_scope` | `false` | The corpus explicitly says this is not handled. The reply is a *useful* refusal — "that's Telstra support, not me, here's what I can do" — and it cites the passage that said so. |
| `not_found` | `false` | Nothing relevant was retrieved. A plain "I don't have anything on that", no citations. |

Refusing cleanly is a success, not an error — it is the behaviour the client demo is
judged on. `out_of_scope` exists because collapsing it either way loses something:
counted as `answered`, the grounding evaluation scores good refusals as inventions;
counted as `not_found`, the avatar throws away a much better reply. A passage is marked
out-of-scope by the heading it sits under (`AIHOLO_OUT_OF_SCOPE_SECTIONS`), so the
boundary lives in the corpus where Anuji owns it, not in the prompt.

`grounded` is kept as `outcome == "answered"` so the Unity side has one boolean to
branch on.

`speechUnavailable` is set, and `audioUrl`/`visemesUrl` are null, when speech is not
configured yet — during bring-up stages 1 and 2 there is no Azure key and the SDK is
not installed. The text answer is still the useful part, so the request succeeds.
Speech that is configured but *failing* is different: that returns `TTS_UNAVAILABLE`.
**Unity should treat a null `audioUrl` as a fallback case regardless of the reason.**

**Field names are camelCase because Unity's `JsonUtility` maps JSON keys to C# field
names literally and cannot rename them.** Do not "tidy" them to snake_case.

### `GET /audio/{id}.wav` · `GET /visemes/{id}.json`

Fetch with `UnityWebRequestMultimedia.GetAudioClip(url, AudioType.WAV)` and a plain
`UnityWebRequest` respectively. The viseme JSON is handed to `VisemePlayer` as raw
text, exactly as `Resources/visemes/<id>.json` is today.

Why URLs instead of inlining the track: one 16-second reply is ~53,000 floats.
Inlining would put a megabyte of JSON through `JsonUtility` on the main thread, and
it would also break the parsing path `VisemePlayer` already has.

### `GET /session/{id}` · `POST /session/{id}/reset`

`GET` returns the turns the model will actually be shown as history — memory is
otherwise invisible, inferable only from whether a follow-up answer looks right, and
that makes a memory bug mysterious instead of diagnosable. An unknown session reads as
empty, not as an error: that is the first question of every conversation.

`POST .../reset` clears one conversation. Wire it to the operator's between-visitors
reset.

`scripts/chat.py` is a terminal client over these endpoints — the quickest way to try
the corpus by hand and the only thing that exercises multi-turn memory interactively.

## 4. Viseme track

Matches `Assets/Team 11/AI_Questions/VisemeTrack.cs` exactly:

```json
{ "id": "a1a09b54eec5", "fps": 60, "shapeCount": 55, "frameCount": 692,
  "values": [ … frameCount * shapeCount floats … ],
  "visemeIds": [ … ], "visemeOffsetsMs": [ … ] }
```

`values` is flattened as `frame * shapeCount + i`. It is flat because `JsonUtility`
cannot parse `float[][]` — do not nest it. The 55 positions are Azure's documented
`BlendShapes` order, which `VisemeShapeMapBuilder` already resolves against Jake's
morph targets by name.

`visemeIds` / `visemeOffsetsMs` are carried for the uLipSync fallback path and for
coarse mouth-shape debugging; the blend shape values are the primary signal.

## 5. Errors

Every failure returns the same envelope, so Unity branches on `code` and never on
prose:

```json
{ "error": { "code": "MODEL_TIMEOUT", "message": "…", "retryable": true } }
```

| Code | HTTP | Retryable | Unity should |
|---|---:|---|---|
| `NO_SPEECH` | 422 | no | Re-prompt: "I didn't catch that." |
| `INVALID_AUDIO` | 415 | no | Bug in the capture path — log it loudly, don't retry. |
| `STT_UNAVAILABLE` | 503 | yes | One retry, then canned fallback. |
| `MODEL_TIMEOUT` | 504 | yes | Straight to canned fallback; do not make the client wait twice. |
| `MODEL_UNAVAILABLE` | 503 | yes | Canned fallback, show "AI offline". |
| `INVALID_MODEL_OUTPUT` | 502 | yes | One retry, then canned fallback. |
| `INDEX_EMPTY` | 503 | no | Operator error — the index was not built. Show it. |
| `TTS_UNAVAILABLE` | 503 | yes | Canned fallback (`IsFallback = true`). |
| `NOT_FOUND` | 404 | no | The artefact was pruned; ask again. |

`retryable` is advice, not permission: Lilan owns the retry budget, because only the
Unity side knows how long the avatar has already been standing there silent.

## 6. Running it

On a Mac, one command does the whole of stage 1 — installs and starts Ollama, pulls
the models, builds the venv and index, calibrates the grounding floor, runs the tests
and smoke-tests the live service:

```bash
cd ~/AI-Hologram-and-Digital-Presence/service
bash scripts/mac_local_setup.sh
```

It is safe to re-run and writes everything it prints to `var/local-setup.log`.

By hand, or on the VX PC:

```bash
cd service
python -m venv .venv && .venv/bin/pip install -r requirements.txt
cp .env.example .env          # then fill in the Azure key
ollama serve &                # embeddings AND inference both need this
ollama pull nomic-embed-text
python -m app.cli index       # build the retrieval index from service/corpus/
python -m app.cli check       # readiness of every component
./run.sh                      # or .\run.ps1 on the VX PC
```

### Bring-up stages

Installed in three steps so the first working loop is minutes away rather than a
multi-gigabyte download away. Each stage closes a different card.

| Stage | Install | Gives you | Card |
|---|---|---|---|
| 1 | `requirements-core.txt` | Real retrieval and real grounded answers; speech still mocked | T14, T15 |
| 2 | `requirements-stt.txt` | Real `/transcribe` | T13 |
| 3 | `requirements-tts.txt` + Azure key | Real voice and real blend shapes | T25 |

### Embeddings come from Ollama

`nomic-embed-text`, not sentence-transformers. Ollama is already required for
inference, so reusing it for embeddings removes torch — about 1.5 GB — from the
install and from the eventual VX PC bundle. The model is asymmetric: passages are
prefixed `search_document: ` and questions `search_query: `. Mixing those up costs
retrieval quality silently, so there is a test pinning it.

An index carries the name of the embedder that built it (`var/index/meta.json`) and
the service refuses to load a mismatched one. Two embedding models produce
incompatible vector spaces, and a mock-built index loading into a real run would
retrieve nonsense with no error anywhere — the worst failure mode this service has.

### How grounding actually works

There are three gates, and the first one is the weakest:

1. **The retrieval floor** (`AIHOLO_GROUNDING_FLOOR`) — a cheap pre-filter that drops
   the genuinely unrelated. Set by `python -m bench.calibrate_floor`.
2. **The corpus** — an explicit "what I cannot help with" section, so a question about
   billing or share price retrieves a passage that *addresses* it rather than the
   nearest loosely-related paragraph.
3. **The model** — asked to classify its own reply as `answered`, `out_of_scope` or
   `not_found` in a dedicated JSON field. This is the real decision.

   It used to be asked to emit a `NOT_IN_CONTEXT` sentinel inside the answer text.
   Measurement killed that too: llama3.1:8b refuses by *paraphrasing* — "I don't have
   anything on that in the material I've been given" — so a string-match classifier
   scored five correct refusals as inventions in a single run. Asking for a field
   instead of a magic string removed the whole class of problem.

   Two guards sit over the model's classification. It cannot claim `out_of_scope`
   unless a passage actually marked out-of-scope was retrieved — the scope boundary
   belongs to the corpus, not to the model's mood. And an unrecognised status becomes
   `not_found`, never `answered`: a garbled reply must fail toward refusing.

   A `not_found` reply is replaced with one fixed sentence rather than the model's own
   wording, so a refusal cannot leak a half-remembered fact in its phrasing.

The floor was originally treated as gate 1 *and* the decision. Measurement on the real
corpus killed that: with `nomic-embed-text`, "What is Telstra's share price today?"
scores **0.717** while "Can I ask you anything I like?" scores **0.511**. Both are about
Telstra, so no absolute cosine threshold separates them — embedding similarity measures
*aboutness*, not *answerability*. Tuning the number harder would only have started
refusing real questions.

So the floor is set just below the weakest question the avatar should answer, and the
refusal decision is measured end to end instead:

```bash
python -m bench.eval_grounding        # service must be running
```

It runs every question through the whole pipeline and reports a confusion matrix. One
box matters more than the rest — *unanswerable → answered* is the avatar inventing
something in front of the client — and the script exits non-zero if anything lands in
it. This is the evidence for T15's "unsupported questions return an explicit not-found
response", and the harness Anuji's 20-question set (T08) plugs into: replace the lists
in `bench/grounding_questions.json` and rerun.

### Mock mode

`AIHOLO_MOCK=true` runs the whole contract with no Ollama, no Azure key and no model
download — real routes, real error codes, real viseme geometry, silent audio of a
plausible length. **This exists so Unity work never waits on the VX PC being free.**

Two honest limits, both measured rather than assumed:

- Mock matching is lexical (see `rag.coverage()`). Hashed-vector retrieval was tried
  first and discarded: across five answerable and five unanswerable questions the
  cosine scores overlapped completely, so no threshold could separate them. Term
  coverage separates the unanswerable set cleanly but refuses some paraphrases a real
  embedder would answer — it errs toward refusing, which is the safe direction.
- Mock audio is silence. Lip-sync *timing* can be checked against it; voice quality
  and Azure's real blend shape values cannot.

Never demo in mock. `/health` reports `"mock": true` precisely so nobody does by accident.

## 7. Status of this document

| Claim | Evidence |
|---|---|
| Contract shape, error codes, viseme flattening, session memory | **Verified** — 17 contract tests pass (`cd service && python -m pytest -q`), plus a live `uvicorn` run exercising `/health`, `/answer`, `/audio`, `/visemes`. |
| Offline behaviour — Ollama down returns `MODEL_UNAVAILABLE` naming the model and URL; a mismatched index is refused | **Verified** — 29 contract tests pass, including the offline paths and both grounding guards. |
| Real answers, retrieval, citations and refusal behaviour against llama3.1:8b + nomic-embed-text | **Verified on a Mac, not on the VX PC** — `bench/eval_grounding.py`, 14 Sept 2026: **16/16**, eight answered, eight refused, nothing invented. Floor `0.54`, corpus 11 chunks. |
| Answer latency | **Verified on a Mac, not on the VX PC** — 4.9–11.3 s per answer, worst on the first call. The demo cannot absorb that silently; `AvatarController.Thinking` exists for it and is currently unused. Baseline for T27. |
| Real transcription quality, Ollama latency, Azure voice and blend shapes, VX PC behaviour | **Simulated — hardware unverified.** No Ollama, Azure key or VX PC was reachable when this was written; the sandbox this was built in has 3 GB RAM, no GPU and a proxied network. `scripts/mac_local_setup.sh` is what produces the first real evidence. T06/T07 benchmarks and Dinesh's T20 matrix close the rest. |
