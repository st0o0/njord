## 1. Merge the stray OpenSpec root

- [x] 1.1 Confirm `openspec/specs/utc-timestamps/` does not exist; `git mv src/openspec/changes/archive/2026-07-30-utc-everywhere openspec/changes/archive/2026-07-30-utc-everywhere`
- [x] 1.2 Create `openspec/specs/utc-timestamps/spec.md` from `openspec/changes/archive/2026-07-30-utc-everywhere/specs/utc-timestamps/spec.md` (`# utc-timestamps Specification`, `## Purpose`, `## Requirements`; keep the four requirements and their scenarios verbatim)
- [x] 1.3 Delete `src/openspec/` (only `config.yaml` with `schema: spec-driven` remains)
- [x] 1.4 `openspec validate utc-timestamps --type spec` passes

## 2. Repair the six failing specs

- [x] 2.1 Delete `openspec/specs/publisher-protocol/` and `openspec/specs/daily-consensus-aggregation/` after a final grep confirming no other spec or doc references them
- [x] 2.2 `openspec/specs/prometheus-metrics/spec.md`: replace the `## ADDED Requirements` header with `## Purpose` (one sentence) + `## Requirements`; keep all requirement text verbatim
- [x] 2.3 `openspec/specs/structural-refactoring/spec.md`: same repair
- [x] 2.4 `openspec/specs/config-builder/spec.md`: add a `#### Scenario` (WHEN/THEN) to "Enrichment toggles with per-feature settings" restating its existing text (consensus, alerts, derived, trends, indices, history toggles; expanded settings pre-filled with defaults)
- [x] 2.5 `openspec/specs/daily-forecast/spec.md`: add SHALL to "Daily parameter count does not affect API call weight" (wording only)
- [x] 2.6 `openspec validate --all --no-interactive` reports 0 failed specs (other in-flight changes may fail independently; they are not this change's concern)

## 3. Local root guard script (prove it red, then green)

- [x] 3.1 Create `scripts/check-openspec-root.sh` as specified in design.md Decision 1 (local tooling only, not under `.github/`)
- [x] 3.2 Prove red: `mkdir -p src/openspec && touch src/openspec/config.yaml && bash scripts/check-openspec-root.sh` exits 1 and prints the path; then remove the temporary files
- [x] 3.3 Prove green: `chmod +x scripts/check-openspec-root.sh` and `bash scripts/check-openspec-root.sh` exits 0 on the cleaned repo

## 4. Config and conventions

- [x] 4.1 `openspec/config.yaml`: add to `rules:` a `tasks` entry "Chunks completable in one session, ordered by dependency."; add a `specs` entry "Specs describe observable behavior; name classes/messages only at interface boundaries." (small and useful); leave `context:` and existing rules unchanged
- [x] 4.2 `AGENTS.md` Conventions: add "Never delete `openspec/changes/archive` (it is tracked, no longer gitignored); OpenSpec lives only in `<repo root>/openspec`"
- [x] 4.3 `AGENTS.md` Build & test: document `openspec validate --all --no-interactive` and `bash scripts/check-openspec-root.sh` (run from the repo root)
- [x] 4.4 `openspec validate openspec-hygiene` passes

## 5. Validation

- [x] 5.1 `bash scripts/check-openspec-root.sh` exits 0
- [x] 5.2 `openspec validate --all --no-interactive` shows no failed specs
- [x] 5.3 `git diff --stat` shows only the files listed in proposal.md Impact and the archive move; nothing under `src/Njord/`, `src/Njord.Tests/` or `.github/`
- [x] 5.4 Commit with a Conventional Commit message (`docs(openspec): repair invalid specs and merge stray root`, `chore: add local openspec root check` if split); do not push

## 6. Follow-up (not part of this change)

- [ ] 6.1 Decide on consolidating overlapping specs (consensus family, `daily-forecast-*`, `health-*`, `grpc-forecast-*`) after reading their contents
- [ ] 6.2 Running these checks in CI: see change `openspec-ci-validation` (blocked, needs a design discussion)
