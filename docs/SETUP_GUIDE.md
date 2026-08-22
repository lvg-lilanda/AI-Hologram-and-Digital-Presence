# Team Setup Guide — AI Hologram and Digital Presence (Telstra muru-D)
For every team member getting set up on this repo. Follow it top to bottom the first time; come back to individual sections later as needed.

## 1. Access
- **Repo (Current):** ask `lvg-lilanda` to add you as a collaborator on the GitHub repo. You'll need a GitHub account and to accept the invite email.
- **VXLab / RACE:** submit the VXLab access request form if you haven't ([Link](https://forms.office.com/r/YMcPnDuCQv)).
- **Repo (later):** assuming gitea.cdirmit.co account are required, we will migrate to it when access is provided and further information will be communicated.

## 2. Core tools
| Tool | Version | Notes |
|---|---|---|
| Git | latest | with Git LFS installed (`git lfs install` once, machine-wide) |
| Unity Hub | latest | manages Editor versions |
| Unity Editor | **6000.4.6f1** | install via Unity Hub — this is the version specified for VXLab Unity projects |
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
git clone https://github.com/<org>/<repo>.git
cd repo
git checkout -b feature/<your-task>
```
 
Full branch/commit/merge rules are in `docs/GIT_WORKFLOW.md` — read it before your first PR.
 
Branch convention: `main` is always the build-ready branch (what gets built and shared, protected — no direct pushes), everyone works in `feature/<short-description>` or `hotfix/<short-description>` and opens a PR back into `main`.

## 5. Running the placeholder

The Unity project itself isn't in the repo yet (still in Research Phase — see the README). This section will be filled in once a placeholder project/scene (e.g. `HoloTV_BaseScene`) is committed, covering how to open it in Unity Hub and get it running on the LookingGlass display.

## 6. Repo hosting & migration (GitHub → gitea)
 
We're starting on GitHub because gitea accounts aren't issued yet. Once they are:
 
1. When gitea accounts are obtained create the repo on `gitea.cdirmit.co`, initialised with the correct `.gitignore`/`.gitattributes` template for Unity (Git LFS needs to be set up at creation — retrofitting it later is painful).
2. Push a mirror of the current GitHub history across:
```
   git remote add gitea https://<username>:<token>@gitea.cdirmit.co/<org>/<repo>.git
   git push gitea --all
   git push gitea --tags
```
