## Why

`AGENTS.md` and `CLAUDE.md` describe `dotnet slopwatch` as a local tool run from
the repo root with a baseline in `.slopwatch/`, and route to the
`dotnet-skills:slopwatch` skill as a quality gate. None of it exists: there is no
`.config/dotnet-tools.json` and no `.slopwatch/`, so `dotnet slopwatch` fails with
"dotnet-slopwatch does not exist". The documented gate is unusable.

## What Changes

- Add `.config/dotnet-tools.json` (repo root) pinning NuGet package
  `slopwatch.cmd` 0.4.2 (command `slopwatch`, verified with `dotnet tool search`
  on 2026-10-02; the plugin skill still shows 0.2.0, which is outdated).
- Add `.slopwatch/baseline.json` created with `dotnet slopwatch init`. A trial run
  against a copy of the current tree found 0 detections (315 files), so the
  baseline is expected to be empty (`"entries": []`) and nothing needs fixing first.
- Correct the wording in `AGENTS.md` and `CLAUDE.md`: exact commands
  (`dotnet tool restore`, `dotnet slopwatch analyze -d .`), run from the repo root.
- No CI or workflow file is added or changed. A CI step is **explicitly deferred**
  and must be discussed with the user first (project policy).
- No application code changes.

## Capabilities

### New Capabilities

None. Tooling only (`skip_specs: true`).

### Modified Capabilities

None.

## Non-goals

- No CI/GitHub Actions wiring (deferred, needs user discussion).
- No Claude Code `PostToolUse` hook in `.claude/settings.json` (can be proposed
  separately).
- No changes to `FunkArr` (has the same gap; out of scope here).
- No custom `.slopwatch/slopwatch.json` rule tuning beyond defaults.
- No changes to the `dotnet-skills` plugin itself.

## Impact

- New files: `.config/dotnet-tools.json`, `.slopwatch/baseline.json`.
- Edited docs: `AGENTS.md` (Build & test), `CLAUDE.md` (Workflow, plugin skills).
- Renovate (`local>st0o0/renovate-config:dotnet`, in `renovate.json`) is expected
  to track the manifest via its nuget manager; to verify, not guaranteed.
- API budget: 0 Open-Meteo requests/month.
