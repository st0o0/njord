## Context

The repository was scaffolded from a template that included Git LFS rules, a full Visual Studio `.gitignore`, and boilerplate configs. Over 22 OpenSpec changes the archive directory grew to 206 tracked files — now 58% of the working tree. The README was written when the project used the Kachelmann API; that was replaced by Open-Meteo in the second change but the README was never updated.

All items are independent file edits with no runtime impact.

## Goals / Non-Goals

**Goals:**
- Accurate README reflecting the current project (Open-Meteo, MQTT Discovery, Docker)
- Minimal `.gitattributes` (line endings only, no LFS)
- Lean `.gitignore` covering only tooling this project actually uses
- Smaller working tree by removing the OpenSpec archive from tracked files
- No dead entries in `.dockerignore` or `.slopwatch/`

**Non-Goals:**
- No CI workflow changes (separate discussion)
- No code, build, or runtime changes
- Not restructuring `openspec/specs/` or the active change workflow

## Decisions

### 1. README content scope

**Decision:** Describe what njord is (Open-Meteo → MQTT → HA), how to run it (docker-compose), how to configure (locations, models), and how to build/test. Keep it under 80 lines — CLAUDE.md is the authoritative dev reference.

**Rationale:** The README is for someone encountering the repo for the first time. CLAUDE.md covers architecture, conventions, and detailed decisions — no need to duplicate.

### 2. Remove LFS entirely vs. keep infrastructure

**Decision:** Remove all LFS filter/diff/merge attributes. Keep only `* text=auto`.

**Rationale:** Zero LFS-tracked objects exist in the entire git history (confirmed via `git lfs ls-files`). LFS adds a CI dependency (needs `git lfs install`) and a server-side requirement for no benefit. If binary assets are ever needed, LFS can be re-added in one commit.

### 3. `.gitignore` approach — curated list vs. template

**Decision:** Write a curated `.gitignore` with only the sections that apply: .NET build output (`bin/`, `obj/`), IDE files (`.vs/`, `.idea/`, Rider), user files (`*.user`, `*.suo`), project-specific custom entries. Drop everything else.

**Rationale:** A 30-line file someone can read in 10 seconds beats a 180-line file where the relevant entries are buried. Removed sections (Python, Click-Once, NCrunch, etc.) have never and will never match a file in this repo.

### 4. OpenSpec archive — git rm + .gitignore

**Decision:** `git rm -r openspec/changes/archive/` then add `openspec/changes/archive/` to `.gitignore`.

**Rationale:** The archive is a read-only historical record. The canonical specs are synced to `openspec/specs/`. Anyone who needs the design history can check `git log -- openspec/changes/archive/` or check out an older commit. Ignoring the path prevents future archives from being accidentally re-committed.

**Alternative considered:** Keep the archive tracked. Rejected because 206 files of pure history inflates search results, file counts, and clone size without serving day-to-day development.

### 5. `.slopwatch/config.json.example` — delete vs. fix

**Decision:** Delete the example file. The slopwatch baseline (`baseline.json`) is what matters; the tool works with defaults and the example's protobuf suppression is misleading for this project.

**Rationale:** A wrong example is worse than no example. If custom suppressions are ever needed, a `config.json` can be created at that point.

## Risks / Trade-offs

- **[Archive removal is one-way in the tree]** → Mitigated: content is preserved in git history. Can be restored with `git checkout <sha> -- openspec/changes/archive/` at any time.
- **[Lean `.gitignore` might miss an edge case]** → Low risk: the curated list covers all tooling in use. CI builds on clean Ubuntu runners, so any missing entry would surface fast.
