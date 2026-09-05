# Feasibility Notes For Shortlisted Concepts

This document consists of technical feasibilty requirements and limitations for each of the shortlisted concepts identified in the Team Workshop on 27th August 2026. 

## Shortlisted Concept - Feasibility Result & Limitations
| # | Concept | Primary user | Additional interaction | Known technical constraint |
|---|---|---|---|---|
| A | Interactive Holographic Presenter | External/internal stakeholders, event presenters | Hand movements (Zooming Into Graphs) | + Graphs not outputting correctly The LookingGlass display has a screen layer that can blur text and fine detail - any concept that shows charts/graphs on-screen needs a different presentation approach (voice narration of figures, or a redesigned low-density visual) rather than a literal chart render. Access to Internal telstra Knowlege base. The leap hand tracker also needs the 2022 version of Unityand will restrict any other plugins not compatible to be used |
| B | Interactive Holographic Product Demonstrator | Customers | Hand movements + haptic sensations to interact with products | AI training / access to Telstra's knowledge base not yet available. Also the one concept that could use haptic feeback, this would require the 2022 version of Unity and will restrict any other plugins not compatible to be used |
| C | Interactive Holographic Telstra Employee Training Coach | Telstra internal team | Hand movements to scroll through FAQ/menu items | AI-training / Telstra knowledge-base access gap as B. The leap hand tracker also needs the 2022 version of Unityand will restrict any other plugins not compatible to be used  |
##

**General Overview of the Concepts**

As per the project requirement, all the concepts will require visual and speech interactions which cannot be compromised. The additional interactions will go on top of the minimum interaction requirements. With the technologies available at the VX Lab, consulation with the Andrew Fenn and the staff of VX Lab was required. Key takeaways was that the technology available is optimized for the 2022 Version for Unity as is recommended to use it. Anyhow, this restricts the usage of latest plugins. If latest plugins are required, more reaserch is required to get the technology in VX Lab to be working with Unity 6

This VX Lab guidance directly shapes what's feasible for concepts A, B and C above — all three currently plan around the Unity 2022 toolchain for exactly this reason, and any concept-specific plugin (haptics for B, or later voice/AI packages) needs to be checked against that constraint before being counted as feasible rather than assumed.

**Note - Unity version requirement:**  **2022.3.40f1** has been choosen to match the VX Lab hardware/plugin stack (LookingGlass, Ultraleap, hap-e). This will not be changed unless client requests or any plugins require it.

## Required Services, Content Formats & Looking Glass Hardware / SDK Access Status

| Item | Status | Notes |
|---|---|---|
| Unity Editor version | Confirmed: 2022.3.40f1 | Per `HoloTV_BaseScene`'s `ProjectVersion.txt`, VX Lab guidance and the team Setup Guide - this is the version the lab's technology stack (LookingGlass, Ultraleap, hap-e) is currently optimised for. Unity 6 (6000.4.6f1, the client's stated default) is not yet supported without further compatibility research |
| LookingGlass Unity Plugin v3.2.0 + LookingGlassBridge 2.5.1 | Installed/importable | Plugin imported per-project via Unity Package Manager; Bridge is a per-machine, system-level driver that must be running in the background for any LookingGlass output at all |
| Ultraleap Unity Plugin 6.15.1 + tracking service (`ultraleap-hand-tracking_v5.20.0-2024.04`) | Installed | Plugin is per-project; the tracking service is per-machine and required for hand-tracking to register independent of the plugin. Sensors currently track hands/arms up to the elbow only — face tracking is unconfirmed and needs further library research |
| `hap-e-unityplugin` haptics (Concept B only) | Available | Requires the older Unity 2022 Editor confirmed above; `sensation-designer_v1.0.1_windows` is the separate standalone authoring tool for haptic patterns, needed only once haptics work starts |
| Gitea repo + LFS | Done | Migrated 2026-08-25 from GitHub via gitea's "Migrate repository" - private, full history/branches/tags, LFS enabled. GitHub repo kept only as a historical copy |
| Voice sample / prompt content format | Open question | Voice cloning tool not yet chosen; |
| Video generation / lip-sync tooling | Not yet validated | `uLipSync`, `Unity-QuestConversationalAI` and Hugging Face identified in Sprint 1 Week 2 as candidate options for lip-sync and conversational AI respectively - none confirmed compatible with the Unity 2022 / VX Lab stack yet |
| Telstra knowledge-base access (Concepts B and C) | Not yet available | Client dependency - needed before B/C can move past "concept" status; AI-training on the knowledge base is the shared blocker for both |
| Physical LookingGlass / VX Lab hardware access | Access provided | Device available in the lab (with infrared sensors + speakers for haptic feedback) |


