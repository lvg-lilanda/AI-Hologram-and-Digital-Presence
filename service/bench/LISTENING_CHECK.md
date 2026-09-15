# Audio listening check

`verify_speech.py` proves the audio is structurally correct — right format, right
length, a facial track that matches and moves. It cannot tell you whether the avatar
sounds *right*, and that is what a client notices in the first five seconds.

Ten minutes with headphones, on the VX PC, through the speakers the demo will use.

Generate the clips by asking through `scripts/chat.py`, then play the files from
`service/var/audio/`.

## Pronunciation — check these first

The corpus is full of names a generic voice gets wrong. Each one is fixable in SSML
if it fails, so test before the client hears it.

| Say this | Listen for |
|---|---|
| "What is Telstra muru-D?" | **muru-D**. Should be "moo-roo-dee". A TTS voice often says "murr-ud" or "myoo-rood". This is the single most likely thing to sound wrong. |
| "Who built you?" | **RMIT** — letters, "are-em-eye-tee", not "rem-it". Team names: Anuji, Hiba, Lilandavaradan, Dinesh, Milindi. |
| "What display do you run on?" | **Looking Glass**, **Ultraleap**, **Unity**. |
| "How does the speech part work?" | **Azure**, **Ollama**, **RAG** if it appears. |

If `muru-D` is wrong, fix it in `app/tts.py` where the SSML is built — wrap the term:

```xml
<phoneme alphabet="ipa" ph="ˈmuːruː diː">muru-D</phoneme>
```

Re-run `bench/verify_speech.py` afterwards; the structural checks must still pass.

## Delivery

- **Pace** — neural voices often read slightly fast for a room. If so, add
  `<prosody rate="-8%">` around the answer.
- **Sentence endings** — does it trail off or land? Abrupt cut-offs usually mean the
  answer text lacks final punctuation.
- **Length** — the prompt asks for two or three sentences. Anything over about
  fifteen seconds is too long to stand and listen to; tighten the corpus, not the voice.
- **The refusal line** — "I don't have anything on that in the material I've been
  given." Played often, so it needs to not sound curt.

## Lip sync — watch, don't just listen

With the avatar on screen:

- **Start together?** Audio and mouth should begin on the same frame.
- **End together?** Drift shows up at the end of a long answer. `verify_speech`
  measures this (0.11 s on the Mac, tolerance 0.35 s) but your eye is the real judge.
- **Does the mouth close between words**, or hang open?
- **Plosives** — "b", "p", "m" should close the lips. Ask something with
  "muru-D" and "prototyping" in it and watch for that.

## Room audio

The lab is not your desk.

- Is it loud enough over the Looking Glass fan and room noise?
- Any clipping at demo volume?
- Standing where a visitor stands — about a metre back — is it intelligible?

## Record the outcome

Note anything you change (voice, rate, phonemes) in the T25 card and rerun
`verify_speech.py`. An SSML change that breaks the viseme timing is exactly the kind
of thing that passes a listening test and fails on screen.
