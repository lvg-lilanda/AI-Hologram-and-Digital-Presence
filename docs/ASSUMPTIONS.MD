# Assumptions

This document consists of technical assumptions which are needed to be confirmed. Update the status column as things get confirmed.

| Item | Current assumption | Status |
|---|---|---|
| Starting codebase |` LookingGlass Unity project, offered as a reference/base — received as the `HoloTV_BaseScene` project (Assets/Packages/ProjectSettings copied into the team repo) | **Received** - committed to team repo as the placeholder application scaffold |
| Display hardware | LookingGlass unit (inferred from `LookingGlass.Unity.Plugin.v3.2.0` + `LookingGlassBridge-2.5.1` in the VXLab software share) | **Confirmed** - but exact model (16" / 32") not yet known |
| Interaction layer | **Confirmed (requirements doc):** two devices available; one has infrared sensors + speakers for haptic feedback. Sensors currently track hands/arms up to the elbow. Face tracking (if needed) needed to be explored. | **Partially confirmed** - current capability is documented, further abilities needed to be explored |
| Haptics (`hap-e-unityplugin`, `sensation-designer`) | **Confirmed** the haptic feedback device requires an **older version of the Unity Editor** than the latest available - the `HoloTV_BaseScene` starter project (which already has hap-e's dependencies noted) is on 2022.3.40f1, matching the corrected Unity Editor version below. Use cases which Haptics are needed is decided. | **Confirmed** - version now tied to `HoloTV_BaseScene`; final version of Unity to be decided based on the plugins |
| Engine choice | Options available to use Unity or Unreal Engine Editor. Team is proceeding with Unity because the entire VXLab software share (LookingGlass, Ultraleap, hap-e) is built around Unity. | **Confirmed** |
| Display text clarity | The LookingGlass screen has a layer that can blur text - any UI-heavy use case idea needs to account for this. | **Confirmed constraint** |
| Use case selection | No final use case has been selected, and the team will not commit to one until primary user, problem, priorities and feasibility are validated. | **Confirmed process** |
| Voice cloning tool | Not yet chosen | **Open** |
| Video generation / lip-sync tool | Not yet chosen | **Open** |
| Unity Editor version | 2022.3.40f1, per the actual `HoloTV_BaseScene` starter project's `ProjectVersion.txt` - planned to be used unless any plugins require the latest version | **Confirmed** |
| Git hosting | Migrated to `gitea.cdirmit.co/2026S2_Projects/AI-Hologram-and-Digital-Presence` (see `SETUP_GUIDE.md`, "Repo hosting & migration"); GitHub copy kept as-is, no longer the primary | **Confirmed** |
| Consenting subject for voice/video clone | Not yet decided - team member vs external member | **Open** |
