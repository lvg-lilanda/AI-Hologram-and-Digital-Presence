# Team 11 - Telstra AI Hologram and Digital Presence

**Status: Research Phase.

An AI integrated digital hologram: displayed via the RMIT VX Lab's LookingGlass unit, with hand-tracking (Ultraleap) and Haptics explored as the interaction layer. This is a native LookingGlass/Unity app, it runs on the LookingGlass hardware in VXLab. Full brief context lives in the team's Project Charter.

## Quick start

New to the repo? Start with `docs/SETUP_GUIDE.md` — it covers tools, the VXLab-specific packages (LookingGlass, Ultraleap, haptics), and how to open and build the placeholder project.

## Project Structure

```
  docs/
    SETUP_GUIDE.md    # full onboarding steps for every team member
    GIT_WORKFLOW.md   # branch model, commit format, merge & tagging rules
  .gitignore      # standard Unity ignores
  README.md
```

## Git Workflow
 
| Branch | Purpose |
|---|---|
| `main` | Protected, no direct pushes |
| `feature/*` | New work → PR back to `main` |
| `hotfix/*` | Urgent fixes → PR back to `main` |
 
Details: [`docs/GIT_WORKFLOW.md`](docs/GIT_WORKFLOW.md).

## Repo hosting

This repo is currently hosted on GitHub while gitea.cdirmit.co accounts are pending. That move is required eventually, since gitea is the project's official server and handles the large binary assets (Unity/LookingGlass/Ultraleap packages) better than GitHub's free tier.

## Team

Team 11 - Telstra AI Hologram and Digital Presence [See Project Charter for the full role breakdown.]
