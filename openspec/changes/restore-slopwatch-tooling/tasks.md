## 1. Tooling

- [ ] 1.1 From repo root run `dotnet new tool-manifest` (creates `.config/dotnet-tools.json`) and `dotnet tool install slopwatch.cmd --version 0.4.2`; confirm the manifest has `"commands": ["slopwatch"]` and `"rollForward": false`
- [ ] 1.2 Run `dotnet tool restore` on a clean checkout state; expect success
- [ ] 1.3 Run `dotnet slopwatch analyze -d . --no-baseline --min-severity info`; expect 0 issues (if not, list findings and decide fix vs baseline before continuing)
- [ ] 1.4 Run `dotnet slopwatch init` (repo root); confirm `.slopwatch/baseline.json` exists and only it (and `config.json.example` if generated) is added

## 2. Documentation

- [ ] 2.1 `AGENTS.md` (Build & test): replace the slopwatch line with `dotnet tool restore` then `dotnet slopwatch analyze -d . --fail-on warning`, run from the repo root, baseline `.slopwatch/baseline.json`
- [ ] 2.2 `CLAUDE.md`: update the Workflow sentence and the `dotnet-skills:slopwatch` bullet with the same exact command; note the manifest version wins over the skill's example version
- [ ] 2.3 Check `renovate.json` / shared preset picks up `.config/dotnet-tools.json`; if it does not, record that as a follow-up (do not edit CI/workflow files)

## 3. Validation

- [ ] 3.1 Clean tree: `dotnet slopwatch analyze -d . --fail-on warning`; expect exit 0
- [ ] 3.2 Inject `catch { }` into a scratch file under `src/Njord.Tests/`; expect exit 1 (SW003); remove it, expect exit 0
- [ ] 3.3 Inject `[Fact(Skip = "x")]` and `#pragma warning disable CS8618`; record whether SW001/SW002 fire; remove them, expect exit 0
- [ ] 3.4 `git status` shows only the new manifest, baseline and two doc edits; no CI/workflow files changed
- [ ] 3.5 Commit with `build: restore slopwatch local tool and baseline` (Conventional Commits); do not push

## 4. Deferred (not in this change)

- [ ] 4.1 Discuss with the user whether and how to add a slopwatch CI step; no workflow edits until agreed
