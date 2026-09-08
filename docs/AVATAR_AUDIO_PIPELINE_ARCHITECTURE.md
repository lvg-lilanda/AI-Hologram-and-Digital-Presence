# Avatar Audio Pipeline — Architecture & Sprint 2 Swap Point

Covers the Sprint 1 deliverable for S1W2 ("Build the avatar audio pipeline"): the avatar
plays a pre-recorded audio clip through a single swappable component, so Sprint 2 can
replace the answer source with a live AI service without touching the scene or the avatar.

## Pipeline

```
question in  ->  answer out  ->  audio + mouth  ->  log  ->  next options
```

Implemented under `Assets/Team 11/AI_Questions/`:

| File | Role |
|---|---|
| `MenuQuestionSource.cs` | Seam B — raises `QuestionAsked` when an option is chosen. Sprint 3 replaces this with a microphone; nothing downstream moves. |
| `IAnswerSource.cs` | **Seam A — the Sprint 2 swap point.** One method: `Task<Answer> AskAsync(string question, CancellationToken ct)`. |
| `CannedAnswerSource.cs` | Sprint 1's implementation of `IAnswerSource`. Reads `Resources/answers.json`, loads `Resources/clips/<id>.wav` and `Resources/visemes/<id>.json`. |
| `AvatarController.cs` | Owns neither seam. Wires question → answer → playback → log → next options. Untouched when either seam is swapped. |
| `VisemePlayer.cs` | Plays the `AudioClip` and drives face blend shapes from the viseme track, sampled against the `AudioSource`'s own playback position. |
| `Answer.cs` | The shared data contract (`Text`, `Audio`, `VisemeJson`, `FollowUps`, `IsFallback`) — identical whether the answer came off disk (Sprint 1) or off the wire (Sprint 2). |

## The swap point

`AvatarController` holds an `IAnswerSource`, not a `CannedAnswerSource`:

```csharp
private IAnswerSource      _answers;
private CannedAnswerSource _canned;
...
_canned  = new CannedAnswerSource();
_answers = _canned;   // Sprint 2 wraps this in FallbackAnswerSource
```

Sprint 2 adds a `LiveAnswerSource` (the real AI service call) and a `FallbackAnswerSource`
that tries `LiveAnswerSource` first and falls back to `_canned` on failure or timeout —
`Answer.IsFallback` already exists in the data contract for exactly this. Everything
downstream of `_answers.AskAsync(...)` — `VisemePlayer`, `AvatarController`,
`OptionMenuUI`, the transcript log — is unaware which implementation answered and
requires no changes.

## Lip sync strategy

Two routes drive the mouth, and the choice between them follows the same answer source
that seam A above already tracks — not a permanent either/or:

- **Viseme track (`VisemePlayer.cs`) — the route for the Sprint 1 demo.** Only works for
  answers that ship a pre-baked `visemes/<id>.json` track alongside the clip, which is
  true for every `CannedAnswerSource` answer and nothing else.
- **`uLipSync` (`com.hecomi.ulipsync`) — the route for when Sprint 2 wires in a live AI
  service.** Drives the same blend shapes from real-time audio analysis instead of a
  pre-baked track, so it works regardless of where the audio came from — the only one of
  the two that can handle an answer nobody pre-recorded a track for.

This is the same seam-A swap already described above: `CannedAnswerSource` → viseme
tracks today, `LiveAnswerSource`/`FallbackAnswerSource` → uLipSync once live answers
exist. `AvatarController`'s own fallback comment anticipates this directly — a clip with
no track is "when uLipSync should take over."

`LipSyncSwitch.cs` (`Assets/Team 11/LipSync/`) gates `uLipSync` and `uLipSyncBlendShape`
behind one bool, defaulting OFF, so flipping it never touches audio playback, the
Animator, or the answer pipeline — none of which know the switch exists. `VisemeShapeMapBuilder.cs`
(same folder) fills `VisemePlayer.ShapeMap` by resolving Azure's 55 named positions
against the avatar's actual blend shapes at runtime, instead of hand-typing indices that
break on reimport.

## Scene wiring

Scene: `Assets/Scenes/Team11 - 3D Model Test.unity`, GameObject `AI`:

- `OptionMenuUI` (builds the option buttons + caption in code)
- `AvatarController` (`Player` → `VisemePlayer`, `Menu` → `MenuQuestionSource`)
- `MenuQuestionSource`
- `VisemePlayer` (`Face` → the avatar's `SkinnedMeshRenderer`)
- `AudioSource` (required by `VisemePlayer`)

The avatar itself changed after this doc was first written: Carla (`rp_carla_rigged_001_yup_a`)
had no blend shapes at all and was replaced with Jake (`Assets/Team 11/M_Model/Jake.fbx`),
a Reallusion Character Creator export with a full `Mouth_*`/`Jaw` blend shape set —
`VisemeShapeMapBuilder`'s name list is calibrated against Jake specifically.

## Build

`ProjectSettings/EditorBuildSettings.asset` now includes `Team11 - 3D Model Test.unity`
as the enabled/first scene, so a Player build boots directly into the avatar instead of
the base-project placeholder scenes.
