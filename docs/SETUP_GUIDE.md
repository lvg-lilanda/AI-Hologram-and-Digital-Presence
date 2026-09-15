# Team Setup Guide — AI Hologram and Digital Presence (Telstra muru-D)
For every team member getting set up on this repo. Follow it top to bottom the first time; come back to individual sections later as needed.

## 1. Access
- **Repo (Current):** `gitea.cdirmit.co/2026S2_Projects/AI-Hologram-and-Digital-Presence` — ask `lvg-lilanda` to add you to the `AIHologramAndDigitalPresenceTeam1` team or as a collaborator directly. You'll need a gitea.cdirmit.co account.
- **VXLab / RACE:** submit the VXLab access request form if you haven't ([Link](https://forms.office.com/r/YMcPnDuCQv)).
- **Repo (old):** the GitHub repo (`github.com/lvg-lilanda/AI-Hologram-and-Digital-Presence`) is kept around as a historical copy but is no longer the primary — clone from gitea going forward.

## 2. Core tools
| Tool | Version | Notes |
|---|---|---|
| Git | latest | with Git LFS installed (`git lfs install` once, machine-wide) |
| Unity Hub | latest | manages Editor versions |
| Unity Editor | **2022.3.40f1** | install via Unity Hub; Version 6000.4.6f1 might be used later on of any/all plugins require it |
| A code editor | your choice | VS Code / Rider both work fine with Unity |

## 3. Project-specific packages
These are the softwares shown and utilized at the VXLab.
| Package | What it is | Scope | Install |
|---|---|---|---|
| `LookingGlass.Unity.Plugin.v3.2.0` | LookingGlass Unity plugin | Per Unity project | Import via Unity's Package Manager |
| `LookingGlassBridge-2.5.1` | LookingGlass runtime driver | Per machine (system-level) | Install once on any machine that needs to output to the physical display. Must be running in the background for LookingGlass output to work at all. |
| `Ultraleap.UnityPlugin-6.15.1` | Ultraleap hand-tracking Unity plugin | Per Unity project | Import via Unity's Package Manager |
| `ultraleap-hand-tracking_v5.20.0-2024.04` | Ultraleap tracking service/runtime | Per machine (system-level) | Install once on any machine with an Ultraleap camera attached. Needed for hand-tracking to register at all, independent of the Unity plugin above. |
| `hap-e-unityplugin` | Haptic device Unity plugin | Per Unity project | Import when haptics work starts |
| `sensation-designer_v1.0.1_windows` | Standalone haptic-authoring app | Per machine, optional | Needed if someone is authoring haptic feedback patterns for the `hap-e` plugin |
| `HoloTV_BaseScene` | VXLab starter Unity scene | Per Unity project | Import as the starting point for the LookingGlass-facing scene — don't build the display scene from a blank Unity scene |
| `UnityBaseScene` | General starter Unity scene | Per Unity project | Baseline project settings/scene VXLab provides for new projects |

## 4. Clone & branch
 
```
git clone https://gitea.cdirmit.co/2026S2_Projects/AI-Hologram-and-Digital-Presence.git
cd AI-Hologram-and-Digital-Presence
git checkout -b feature/<your-task>
```
 
Full branch/commit/merge rules are in `docs/GIT_WORKFLOW.md` — read it before your first PR.
 
Branch convention: `main` is always the build-ready branch (what gets built and shared, protected — no direct pushes), everyone works in `feature/<short-description>` or `hotfix/<short-description>` and opens a PR back into `main`.

## 5. Running the placeholder

The `HoloTV_BaseScene` project (`Assets/`, `Packages/`, `ProjectSettings/`) is committed at the repo root. To run it:
1. Open Unity Hub → **Add** → point at the repo root (where `Assets/` lives)
2. Unity Hub should offer to install **2022.3.40f1** if you don't already have it — accept
3. Open the project; Unity will regenerate `Library/` on first open, which takes a while
4. Open the relevant scene under `Assets/Scenes` to see the placeholder

## 6. Repo hosting & migration (GitHub → gitea) — done

Migrated on 2026-08-25 via gitea's "Migrate repository" (from GitHub, LFS support enabled, private, full history/branches/tags). Repo now lives at `gitea.cdirmit.co/2026S2_Projects/AI-Hologram-and-Digital-Presence`; the GitHub repo is kept as a historical copy only.

If you have an existing local clone from GitHub, repoint your `origin` remote instead of re-cloning:
```
git remote set-url origin https://gitea.cdirmit.co/2026S2_Projects/AI-Hologram-and-Digital-Presence.git
git fetch origin
```
