## 1. Tooling

- [x] 1.1 From repo root run `dotnet new tool-manifest` (creates `dotnet-tools.json` at the repo root on this SDK, moved to `.config/dotnet-tools.json`) and `dotnet tool install slopwatch.cmd --version 0.4.2`; confirm the manifest has `"commands": ["slopwatch"]` and `"rollForward": false`
- [x] 1.2 Run `dotnet tool restore` on a clean checkout state; expect success
- [x] 1.3 Run `dotnet slopwatch analyze -d . --no-baseline --min-severity info`; expect 0 issues (0 found; if not, list findings and decide fix vs baseline before continuing)
- [x] 1.4 Run `dotnet slopwatch init` (repo root); confirm `.slopwatch/baseline.json` exists and only it (and `config.json.example` if generated) is added

## 2. Documentation

- [x] 2.1 `AGENTS.md` (Build & test): replace the slopwatch line with `dotnet tool restore` then `dotnet slopwatch analyze -d . --fail-on warning`, run from the repo root, baseline `.slopwatch/baseline.json`
- [x] 2.2 `CLAUDE.md`: update the Workflow sentence and the `dotnet-skills:slopwatch` bullet with the same exact command; note the manifest version wins over the skill's example version
- [x] 2.3 Check `renovate.json` / shared preset picks up `.config/dotnet-tools.json`; if it does not, record that as a follow-up (do not edit CI/workflow files)
  - Result: shared presets (st0o0/renovate-config `dotnet.json` + `default.json`, read via raw.githubusercontent) extend `config:recommended` and do not disable the nuget manager; Renovate's nuget manager matches `.config/dotnet-tools.json` by default and the minor/patch automerge rule applies. Verified by inspection of the preset, not by an actual Renovate run.

## 3. Validation

- [x] 3.1 Clean tree: `dotnet slopwatch analyze -d . --fail-on warning`; expect exit 0
- [x] 3.2 Inject `catch { }` into a scratch file under `src/Njord.Tests/`; expect exit 1 (SW003); remove it, expect exit 0
- [x] 3.3 Inject `[Fact(Skip = "x")]` and `#pragma warning disable CS8618`; record whether SW001/SW002 fire; remove them, expect exit 0
  - Result: SW002 fires on `#pragma warning disable`. SW001 does NOT fire for `[Fact(Skip = ...)]` in `*Spec.cs`: the rule only inspects files named `*Tests.cs` (probed: `FooTests.cs` detected, `FooSpec.cs` not; `-p`/`-f` do not help; `[Ignore]` never detected). All 109 project specs are therefore not covered; documented in AGENTS.md. Follow-up: report upstream or add a `*Tests.cs`-named canary/other guard.
- [x] 3.4 `git status` shows only the new manifest, baseline and two doc edits; no CI/workflow files changed
- [x] 3.5 Commit (split into 3 commits: `build: pin slopwatch as a local dotnet tool`, `chore: add slopwatch baseline`, `docs: document slopwatch commands`) (Conventional Commits); do not push

## 4. Deferred (not in this change)

- [ ] 4.1 Discuss with the user whether and how to add a slopwatch CI step; no workflow edits until agreed
