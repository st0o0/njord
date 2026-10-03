## Context

See proposal.md. Findings from running the checks locally (OpenSpec CLI `1.7.0`, package `@fission-ai/openspec`; a global `openspec@0.0.0` also exists in `npm ls -g` and must not be confused with it):

- `openspec validate --all`: re-run after `spec-drift-cleanup` was archived: the same **6 specs still fail** (other in-flight changes may fail independently and are out of scope):
  - `config-builder`: requirement "Enrichment toggles with per-feature settings" has no scenario.
  - `daily-consensus-aggregation`: zero requirements (every requirement is an HTML-commented REMOVED block; the capability no longer exists).
  - `daily-forecast`: requirement "Daily parameter count does not affect API call weight" lacks SHALL/MUST.
  - `prometheus-metrics`, `structural-refactoring`: files start with `## ADDED Requirements` (unsynced delta), no `## Purpose`.
  - `publisher-protocol`: zero requirements, "Deprecated" stub superseded by `egress-event`.
- Two roots exist: `openspec/` (canonical, 88 specs) and `src/openspec/` (config `schema: spec-driven` only, one archived change `2026-07-30-utc-everywhere` whose `utc-timestamps` delta was never synced; no `openspec/specs/utc-timestamps` exists).
- `openspec/changes/archive/` is tracked and populated (commit `19f0439` earlier deleted ~22 archived changes; the archive is no longer gitignored).
- CI automation of these checks lives in the separate, blocked change `openspec-ci-validation`.

## Goals / Non-Goals

**Goals:**
- One OpenSpec root, all main specs valid, and a local one-command way to verify both.

**Non-Goals:**
- CI/workflow changes, spec consolidation, FunkArr edits, restoring deleted archives.

## Decisions

### 1. Root guard as a local script

`scripts/check-openspec-root.sh` (local tooling, not under `.github/`): `find` pruning `node_modules`, `.git`, `bin`, `obj`, `dist`; reports any directory named `openspec` or OpenSpec config file (`openspec/config.yaml|yml`) outside `<repo root>/openspec`; on hits prints `OpenSpec must live only in <repo root>/openspec. Found: <paths>` and exits 1. Runs locally with `bash scripts/check-openspec-root.sh`. Documented in `AGENTS.md` Build & test next to `openspec validate --all --no-interactive`. Not wired into CI (see `openspec-ci-validation`).

### 2. Spec repairs (format, not semantics)

| Spec | Repair |
|---|---|
| `publisher-protocol` | delete dir (stub; replacement `egress-event` exists; verified no other file references it) |
| `daily-consensus-aggregation` | delete dir (all requirements removed; `DailyConsensus` replaces it) |
| `prometheus-metrics`, `structural-refactoring` | move body under `## Requirements`, add `## Purpose` (one sentence derived from the requirements); drop the `ADDED` header |
| `config-builder` | add one `#### Scenario` to "Enrichment toggles with per-feature settings" restating its existing text as WHEN/THEN |
| `daily-forecast` | change "Only hourly variable count determines the API call weight" to "... SHALL determine ..." (wording only; the scenario already states the behavior) |

Deleting stubs removes their spec directory, which `openspec validate` would otherwise keep failing on. *Alternative:* keep stubs with a dummy requirement. Rejected: invents requirements to satisfy a tool.

### 3. Merging the stray root

`git mv src/openspec/changes/archive/2026-07-30-utc-everywhere openspec/changes/archive/`; create `openspec/specs/utc-timestamps/spec.md` from the change's delta using sync semantics (`## ADDED Requirements` becomes main-spec `## Purpose` + `## Requirements`); delete `src/openspec/` (its `config.yaml` is only `schema: spec-driven`). Verify with the guard script.

### 4. Config rules

Add to `openspec/config.yaml` `rules:`: `tasks`: "Chunks completable in one session, ordered by dependency." (FunkArr rule); `specs`: "Specs describe observable behavior; name classes/messages only at interface boundaries." Existing `proposal` and `tasks` rules stay. A `design` rule is not added (no clearly useful small rule). The archive practice ("never delete `openspec/changes/archive`") is documented in `AGENTS.md` Conventions (it exists now), together with the note that the archive is tracked, not in `config.yaml`.

## Risks / Trade-offs

- [Guard false positive on an unrelated `openspec` folder] → explicit exclusion list, extend by code change.
- [Local check can be forgotten without CI] → accepted until `openspec-ci-validation` is decided.

## Migration Plan

Root merge and repairs first, then the local script and docs. Validate locally. Rollback: revert the commit.
