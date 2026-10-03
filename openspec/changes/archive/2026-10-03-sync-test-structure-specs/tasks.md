## 1. Specs

- [x] 1.1 Restate the affected requirements of `test-project-structure` and `architecture-zone-enforcement` as delta specs under `specs/`
- [x] 1.2 Verify every named project, file and symbol in the deltas against `src/` (`src/Njord.slnx`, `src/Njord.Architecture.Tests/*`)

## 2. Docs check

- [x] 2.1 Grep `AGENTS.md`, `CLAUDE.md`, `.claude/skills/njord-*/SKILL.md` and `openspec/config.yaml` for stale single-project claims and verify the referenced paths exist
- [x] 2.2 Fix any stale reference found (only `openspec/config.yaml` task rule; AGENTS.md, CLAUDE.md and skills were already correct)

## 3. Validation

- [x] 3.1 `openspec validate sync-test-structure-specs`
- [x] 3.2 After archive: `openspec validate --all --no-interactive` and `bash scripts/check-openspec-root.sh`
- [x] 3.3 Grep `openspec/specs` for `Njord.Tests` and confirm each remaining hit is correct
