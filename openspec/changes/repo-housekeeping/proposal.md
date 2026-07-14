## Why

The repository carries accumulated hygiene debt from its early setup: the README still describes Kachelmann (replaced by Open-Meteo), Git LFS is configured for image types that don't exist, `.gitignore` is a 180-line Visual Studio mega-template with sections that will never apply, and the OpenSpec archive accounts for 58% of all tracked files despite being fully preserved in git history. None of these cause runtime issues, but they create noise for contributors and inflate the working tree unnecessarily.

## What Changes

- **README.md** — rewrite to reflect Open-Meteo as the data source, mention HA MQTT Discovery integration, Docker deployment via `docker-compose.yml`, and configurable locations/models.
- **`.gitattributes`** — remove all LFS tracking rules (zero binary assets in the repo); keep only `* text=auto` for line-ending normalization.
- **`.gitignore`** — trim from ~180 lines to ~30 relevant entries by removing sections for Python, Click-Once, MonoDevelop, NCrunch, StyleCop, Microsoft Fakes, TeamCity, NTVS, old-style NuGet packages, and other tooling this project doesn't use.
- **OpenSpec archive** — `git rm` the `openspec/changes/archive/` tree (206 files) and add the path to `.gitignore`. Design history is preserved in git; synced specs live in `openspec/specs/`.
- **`.dockerignore`** — remove the `docs` entry (no such directory exists).
- **`.slopwatch/config.json.example`** — remove the protobuf/gRPC suppression rule that doesn't apply, or delete the example file entirely (slopwatch works fine with defaults).

## Non-goals

- No code changes, no behavior changes, no CI workflow modifications.
- Not changing the OpenSpec spec-driven schema or active change workflow.
- Not restructuring the `src/` directory or project layout.

## Capabilities

### New Capabilities

_(none — this is a housekeeping change with no new capabilities)_

### Modified Capabilities

_(none — no spec-level behavior changes)_

## Impact

- **Tracked file count**: drops from ~357 to ~150 (removing 206 archive files).
- **Working tree**: cleaner navigation and search results.
- **Git LFS**: can be fully uninstalled from the repo after this change (no LFS-tracked objects in history either, since no images were ever committed).
- **No runtime, build, or CI impact** — all changes are to repo metadata files.
