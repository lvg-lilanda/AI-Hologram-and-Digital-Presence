# Git Workflow
 
A single `main` branch — no `develop`, no release branches. Simple enough for a capstone team:
branch, build, PR, merge.

## Branch Structure
 
```
main         ← build-ready (protected)
  ↑
feature/*    ← new work (branched from main, PR back to main)
hotfix/*     ← urgent fixes (branched from main, PR back to main)
```

## Branch Naming
 
| Type | Pattern | Example |
|------|---------|---------|
| Feature | `feature/{kebab-case}` | `feature/looking-glass-placeholder-scene` |
| Hotfix | `hotfix/{kebab-case}` | `hotfix/build-broken-missing-plugin-ref` |

## Workflow

```
git checkout main && git pull
git checkout -b feature/{name}
# ...make changes, commit...
git push -u origin feature/{name}
gh pr create --base main
# review, merge, GitHub deletes the branch automatically
```

`/git-feature` and `/git-hotfix` (Claude Code skills) automate exactly this. There's no
functional difference between the two branch types — `hotfix/*` is just a naming convention to
flag "this is an urgent fix" to reviewers.

## Commit Messages (Conventional Commits)

The `commit-msg` hook enforces this format:

```
type(scope): description

Examples:
feat: add notes feature
fix(auth): handle token expiry on refresh
docs: update Firestore schema for notes
refactor(backend): extract auth middleware
test: add integration tests for health route
chore: upgrade firebase-admin to v13
```

**Types:** `feat` · `fix` · `docs` · `style` · `refactor` · `test` · `chore` · `build` · `ci` · `perf` · `revert`

## Merge Strategy

Squash merge every PR into `main` — keeps history linear and each merge maps to one logical
change.

## Protected Branch

`main` is protected — no direct pushes. All changes go through a pull request.