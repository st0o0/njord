## 1. Merge the stray OpenSpec root

- [ ] 1.1 Confirm `openspec/specs/utc-timestamps/` does not exist; `git mv src/openspec/changes/archive/2026-07-30-utc-everywhere openspec/changes/archive/2026-07-30-utc-everywhere`
- [ ] 1.2 Create `openspec/specs/utc-timestamps/spec.md` from `openspec/changes/archive/2026-07-30-utc-everywhere/specs/utc-timestamps/spec.md` (`# utc-timestamps Specification`, `## Purpose`, `## Requirements`; keep the four requirements and their scenarios verbatim)
- [ ] 1.3 Delete `src/openspec/` (only `config.yaml` with `schema: spec-driven` remains)
- [ ] 1.4 `openspec validate utc-timestamps --type spec` passes

## 2. Repair the six failing specs

- [ ] 2.1 Delete `openspec/specs/publisher-protocol/` and `openspec/specs/daily-consensus-aggregation/` after a final grep confirming no other spec or doc references them
- [ ] 2.2 `openspec/specs/prometheus-metrics/spec.md`: replace the `## ADDED Requirements` header with `## Purpose` (one sentence) + `## Requirements`; keep all requirement text verbatim
- [ ] 2.3 `openspec/specs/structural-refactoring/spec.md`: same repair
- [ ] 2.4 `openspec/specs/config-builder/spec.md`: add a `#### Scenario` (WHEN/THEN) to "Enrichment toggles with per-feature settings" restating its existing text (consensus, alerts, derived, trends, indices, history toggles; expanded settings pre-filled with defaults)
- [ ] 2.5 `openspec/specs/daily-forecast/spec.md`: add SHALL to "Daily parameter count does not affect API call weight" (wording only)
- [ ] 2.6 `openspec validate --all --no-interactive` reports 0 failures

## 3. Root guard script (prove it red, then green)

- [ ] 3.1 Create `.github/scripts/check-openspec-root.sh` as specified in design.md Decision 3 (same content as FunkArr's `.github/scripts/check-openspec-root.sh`; if that file exists by now, copy it)
- [ ] 3.2 Prove red: `mkdir -p src/openspec && touch src/openspec/config.yaml && bash .github/scripts/check-openspec-root.sh` exits 1 and prints the path; then remove the temporary files
- [ ] 3.3 Prove green: `chmod +x` and `bash .github/scripts/check-openspec-root.sh` exits 0 on the cleaned repo

## 4. Workflow

- [ ] 4.1 Create `.github/workflows/openspec.yml`: `on: pull_request` and `workflow_dispatch` (no `paths` filter); `permissions: contents: read`; `concurrency` as in `.github/workflows/ci.yml`; one `ubuntu-latest` job, `timeout-minutes: 5`
- [ ] 4.2 Steps: `actions/checkout@v7`; `actions/setup-node` (pinned LTS); `npm install -g @fission-ai/openspec@1.7.0` with a comment that the pin matches the local CLI; `bash .github/scripts/check-openspec-root.sh`; `openspec validate --all --no-interactive`
- [ ] 4.3 Lint the workflow locally if `actionlint` is available; otherwise review against `.github/workflows/ci.yml` structure

## 5. Config and conventions

- [ ] 5.1 `openspec/config.yaml`: add to `rules:` a `tasks` entry "Chunks completable in one session, ordered by dependency." and a `specs` entry "Specs describe observable behavior; name classes/messages only at interface boundaries."; leave `context:` and existing rules unchanged
- [ ] 5.2 If `AGENTS.md` exists (after `restructure-agent-docs`), add under Conventions: "Never delete `openspec/changes/archive`; OpenSpec lives only in `<repo root>/openspec`"; otherwise record it as a follow-up in this change's design.md
- [ ] 5.3 `openspec validate openspec-hygiene` passes

## 6. Validation

- [ ] 6.1 `bash .github/scripts/check-openspec-root.sh` exits 0
- [ ] 6.2 `openspec validate --all --no-interactive` exits 0 with 0 failed items
- [ ] 6.3 `git diff --stat` shows only the files listed in proposal.md Impact and the archive move; nothing under `src/Njord/` or `src/Njord.Tests/`
- [ ] 6.4 Sanity run from `src/` (no suites touched, confirms nothing else changed): `dotnet build Njord.slnx` and `dotnet run --project Njord.Tests/Njord.Tests.csproj`
- [ ] 6.5 Commit with a Conventional Commit message (`ci: validate openspec and enforce single root`, plus `docs(openspec): repair invalid specs` if split); do not push

## 7. Follow-up (not part of this change)

- [ ] 7.1 Decide on consolidating overlapping specs (consensus family, `daily-forecast-*`, `health-*`, `grpc-forecast-*`) after reading their contents
- [ ] 7.2 Extract the guard + validate steps into a reusable workflow in `st0o0/github-workflows` once both repos run identical files
