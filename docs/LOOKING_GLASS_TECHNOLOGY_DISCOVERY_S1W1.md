# Looking Glass Technology Discovery and Technical Research

**Owner:** Dinesh (Dev2)  
**Track:** Infrastructure  
**Week:** 1  
**Sprint effort:** 4 hours  
**Status:** Complete, pending VXLab hardware confirmation

## Purpose

This discovery records the real VXLab stack introduced to the team: a Looking Glass light-field display driven by a Unity application, with Ultraleap infrared hand tracking and mid-air haptics. It supersedes the earlier Nova-device assumption for this infrastructure investigation.

The core decision is not the engine or display: those are determined by the lab. The decision is whether the final experience includes haptics, or treats it as an optional enhancement over the visual and hand-tracking experience.

## Stack Overview

| Layer | Technology | Role |
| --- | --- | --- |
| Display | Looking Glass light-field display | Presents autostereoscopic 3D without a headset or glasses. |
| Application | Unity + Universal Render Pipeline (URP) | Authors and runs the interactive scene. |
| Display integration | Looking Glass Unity Plugin + Looking Glass Bridge | Produces and sends the multi-view render to the physical display. |
| Input | Ultraleap infrared hand tracker + Unity plugin | Supplies hands, joints, pinch, grab and pose input. |
| Haptics | `hap-e` Unity plugin and Sensation Designer, subject to availability | Adds mid-air tactile feedback to interactions. |

The display renders the scene from many virtual viewpoints per frame. This creates the light-field effect but makes performance, depth placement, and real-device testing materially more important than for a conventional Unity monitor build.

## Content and Delivery Options

| Area | Option 1: Full stack | Option 2: Reduced stack |
| --- | --- | --- |
| Display | Looking Glass | Looking Glass |
| Engine | Unity | Unity |
| Input | Ultraleap hand tracking | Ultraleap hand tracking |
| Haptic feedback | Mid-air haptics | Visual and audio feedback only |
| User experience | Users can see, manipulate, and feel virtual objects. | Users can see and manipulate virtual objects without tactile feedback. |
| Hardware dependency | Display, tracker, and haptics array. | Display and tracker only. |
| Integration effort | Three coordinated subsystems. | Two coordinated subsystems. |
| Main risk | Haptics availability, calibration, and hardware-only testing. | Less novel tactile experience. |
| Best fit | Tactile feedback is central to the selected concept. | Haptics is unavailable, unreliable, or not central to the concept. |

### Preliminary recommendation

Target Option 1, but implement the visual and hand-tracking interaction as a complete core that works without haptics. Haptic feedback should be additive: disabling the device or SDK must not prevent users from completing the interaction. A hardware-in-the-loop proof of concept should validate both modes early in Sprint 1 or Sprint 2 before the team commits to haptics as a final requirement.

## Content Pipeline and Display Constraints

Unity scenes can use normal imported assets. Use FBX as the default interchange format; glTF and OBJ are suitable where they better fit the source asset or a lighter static model.

```
3D modelling tool -> FBX/glTF export -> Unity import -> materials and textures
-> placement in hologram volume -> hand interaction -> VXLab display test
```

Looking Glass-specific constraints:

- Place important objects inside the hologram camera volume; objects outside it can appear flat or clipped.
- Optimise polygon counts, textures, lights, real-time shadows, effects, animation, and physics because multi-view rendering amplifies scene cost.
- Use depth separation, lighting, and parallax deliberately so objects read as floating rather than as flat-screen content.
- Position touch targets inside the overlap of the display's hologram volume and the hand tracker's tracking volume. For Option 1, they must also fit the haptic interaction volume and retain line of sight to the array.
- Keep UI sparse and test its legibility on the physical screen, as VXLab notes that the display layer can blur text.

## Confirmed Project Baseline

The repository setup guide records the VXLab-provided versions below. They are the project baseline until the VXLab team confirms otherwise; they take precedence over generic vendor documentation or beta-plugin recommendations.

| Component | Project baseline | Status |
| --- | --- | --- |
| Unity Editor | `6000.4.6f1` | Confirmed in project requirements and setup guide |
| Render pipeline | URP compatible with the provided base scene | Confirm exact URP package version in starter project |
| Looking Glass Unity Plugin | `3.2.0` | VXLab package provided |
| Looking Glass Bridge | `2.5.1` | VXLab package provided |
| Ultraleap Unity Plugin | `6.15.1` | VXLab package provided |
| Ultraleap tracking runtime | `5.20.0-2024.04` | VXLab package provided |
| Haptics integration | `hap-e-unityplugin` | Confirm device and availability |
| Haptic authoring | Sensation Designer `1.0.1` | Optional; needed to author patterns |
| Starter scene | `HoloTV_BaseScene` | Use rather than creating the display scene from blank |

The final holographic delivery target is a standalone Unity build on the computer connected to the Looking Glass, with Bridge and the required hardware runtimes installed. A Unity WebGL build could be a flat, non-holographic demonstration aid only; it is not a delivery target for the Looking Glass experience.

## Hardware, Access, and Compatibility

| Area | Requirement or constraint | Verification action |
| --- | --- | --- |
| Display model | Model, native resolution, hologram volume, and driving-PC specification are unknown. | Confirm with VXLab. |
| Host computer | Windows with a dedicated GPU or supported Apple Silicon Mac; final build must match the actual lab host. | Test the supplied lab machine. |
| Bridge | Must be installed and running on the host that drives the display. | Confirm device detection and preview. |
| Hand tracker | Exact Ultraleap model and mounting position are unknown. | Confirm model, tracking volume, and runtime detection. |
| Haptics | Demonstrated, but project-long availability is unconfirmed. | Confirm device model, booking access, calibration process, and availability. |
| Lab access | Hardware iteration depends on lab access and booking. | Confirm hours, booking rules, supervision, safety, and hygiene requirements. |
| Asset compatibility | FBX preferred; imported material and texture behaviour must be checked in Unity. | Import representative assets into the base scene. |

## Skills and Onboarding

Developers need Unity scenes, GameObjects, components, prefabs, materials, C#, package management, URP, Git, the Looking Glass plugin and Bridge, Ultraleap hand interactions, standalone builds, and haptics tooling for Option 1.

UX contributors need to understand hologram, tracking, and haptic-volume overlap; mid-air gestures such as reach, pinch, grab, and push; holographic depth and legibility; and accessibility for standing, physically present users.

The BA should write testable requirements that identify the gesture, expected visual and haptic response, hardware preconditions, safe interaction zone, and error handling for lost tracking or unavailable haptics.

### Onboarding checklist

1. Install Unity Hub and Unity `6000.4.6f1`; confirm licensing.
2. Clone the repository and open the project with the agreed Editor version.
3. Install or import the agreed VXLab packages and confirm package resolution without errors.
4. Install Bridge and the Ultraleap runtime on the relevant host machines.
5. Open `HoloTV_BaseScene`, run it in the Unity Editor, and verify Git workflow access.
6. In VXLab, confirm Looking Glass output, Ultraleap hand detection, and a hand rig in scene.
7. Import an FBX asset, keep it within the hologram volume, and implement one pinch or grab interaction.
8. For Option 1, verify the haptics device and trigger one test sensation.
9. Build and launch a standalone application against Bridge.
10. Log setup, calibration, and compatibility issues in the project tracker.

## Prototype Performance Test

The first prototype must be run on the actual Looking Glass, not assessed only in the Unity Editor. It should contain a representative scene, a small set of interactive objects, expected gestures, near-final lighting/materials, and any key animation or effect. Under Option 1 it must include one haptic sensation attached to an interactive object.

Measure or record frame stability, display depth readability, hand-tracking reliability, haptic alignment, setup/calibration time, and failure behaviour when hardware is disconnected or a hand leaves the interaction volume.

## Open Questions for VXLab

- Which Looking Glass model is installed, and what are its resolution, hologram volume, and host-PC requirements?
- Which Ultraleap tracker is mounted, and is it fixed or moveable?
- Is the haptics array available throughout the project, and is it the device supported by `hap-e-unityplugin`?
- Are Bridge and the package versions above already installed and configured on the lab machine?
- What are the lab booking, supervision, safety, and hygiene rules?
- Can the team inspect prior Looking Glass Unity projects as reference?
- What host machine and delivery format will be used at assessment: live demo, build, recording, or all three?
- Is the intended viewing mode a single active user or a small group?
- What Unity, C#, XR, and Looking Glass experience does each team member have?
- Does assessment require any content format besides the native interactive Unity scene?

## Completion Comment

Week 1 infrastructure technology discovery is complete. The investigation compares two viable delivery approaches: a full Looking Glass, Unity, Ultraleap hand-tracking, and mid-air-haptics stack; and a resilient visual plus hand-tracking fallback. It documents the lab-aligned software baseline, access and hardware dependencies, performance constraints, content compatibility, training requirements, delivery limitations, open VXLab questions, and the preliminary recommendation to make haptics additive over a fully working visual and hand-tracking core. Final haptics commitment remains contingent on an early hardware-in-the-loop proof of concept and confirmed VXLab availability.

## Evidence and References

- [Looking Glass Unity Plugin](https://lookingglassfactory.com/software/looking-glass-unity-plugin)
- [Looking Glass Bridge 2.0](https://lookingglassfactory.com/blog/looking-glass-bridge-2-0-a-new-chapter)
- [Using Unity with Looking Glass](https://lfdocs.lookingglassfactory.com/software/index/tutorial)
- [Ultraleap Unity Plugin](https://docs.ultraleap.com/xr-and-tabletop/xr/unity/plugin/index.html)
- [Ultraleap mid-air haptics documentation](https://docs.ultraleap.com/haptics/index.html)
- [Unity: Hand Tracking and Mid-Air Haptics](https://unity.com/resources/hand-tracking-haptics-product)
- [Project setup guide](SETUP_GUIDE.md)
- [Sprint 1 Week 1 requirements](REQUIREMENTS_DOCUMENT_S1W1.md)