## Context

See proposal.md. Findings from running the checks locally (OpenSpec CLI `1.7.0`, package `@fission-ai/openspec`; a global `openspec@0.0.0` also exists in `npm ls -g` and must not be confused with it):

- `openspec validate --all`: 92 items, **86 passed, 6 failed**, all pre-existing specs (no change is invalid):
  - `config-builder`: requirement "Enrichment toggles with per-feature settings" has no scenario.
  - `daily-consensus-aggregation`: zero requirements (every requirement is an HTML-commented REMOVED block; the capability no longer exists).
  - `daily-forecast`: requirement "Daily parameter count does not affect API call weight" lacks SHALL/MUST.
  - `prometheus-metrics`, `structural-refactoring`: files start with `## ADDED Requirements` (unsynced delta), no `## Purpose`.
  - `publisher-protocol`: zero requirements, "Deprecated" stub superseded by `egress-event`.
- Two roots exist: `openspec/` (canonical, 88 specs) and `src/openspec/` (config `schema: spec-driven` only, one archived change `2026-07-30-utc-everywhere` whose `utc-timestamps` delta was never synced; no `openspec/specs/utc-timestamps` exists).
- `openspec/changes/archive/` exists but is empty; commit `19f0439` deleted ~22 archived changes.
- CI: `.github/workflows/ci.yml` only calls reusable workflows from `st0o0/github-workflows@main`; `commitlint.yml` there uses plain `actions/checkout@v7` (tag, not SHA). No `.github/scripts/` directory exists.
- FunkArr change `openspec-ci-validation` (not yet applied) defines the same guard + a separate `openspec.yml`; it makes the specs step report-only because 17 of its 170 specs fail. njord can be stricter because only 6 fail and all are fixable here.

## Goals / Non-Goals

**Goals:**
- A PR that creates a second OpenSpec root, an invalid change or an invalid spec fails CI.
- The same guard script and workflow shape as FunkArr, so the two repos stay interchangeable.

**Non-Goals:**
- Spec consolidation, FunkArr edits, restoring deleted archives.

## Decisions

### 1. Separate `openspec.yml` workflow, mirroring FunkArr

`ci.yml` is a list of reusable-workflow calls; a repo-specific check with a CLI install does not belong there. New workflow: `on: pull_request` and `workflow_dispatch`, **no `paths` filter** (a stray root can appear anywhere, so filtering on `openspec/**` would miss exactly the case the guard exists for), top-level `permissions: contents: read`, `concurrency` as in `ci.yml`, one `ubuntu-latest` job with `timeout-minutes: 5`.
*Alternative:* a shared reusable workflow in `st0o0/github-workflows`. Deferred: it is an outward-facing change in another repo, and FunkArr already chose the per-repo file. Can be extracted later once both repos run the same file.

### 2. Pinning

`actions/checkout@v7` (same tag style as `commitlint.yml`; Renovate manages the pins), `actions/setup-node` with an LTS Node version (njord has no Node toolchain, so pick the current LTS and pin it), `npm install -g @fission-ai/openspec@1.7.0`. The exact package name is spelled in the workflow and a comment states the pin matches the local CLI.

### 3. Root guard as a script

`.github/scripts/check-openspec-root.sh`: `find` pruning `node_modules`, `.git`, `bin`, `obj`, `dist`; reports any directory named `openspec` or OpenSpec config file (`openspec/config.yaml|yml`) outside `<repo root>/openspec`; on hits prints `OpenSpec must live only in <repo root>/openspec. Found: <paths>` and exits 1. Runs locally with `bash .github/scripts/check-openspec-root.sh`. Content kept identical to FunkArr's.

### 4. `openspec validate --all` is blocking from day one

Possible because the six failures are repaired in this change. Order matters: repairs land **before** the workflow in the same change/commit sequence so CI is never red on `main`.

### 5. Spec repairs (format, not semantics)

| Spec | Repair |
|---|---|
| `publisher-protocol` | delete dir (stub; replacement `egress-event` exists; verified no other file references it) |
| `daily-consensus-aggregation` | delete dir (all requirements removed; `DailyConsensus` replaces it) |
| `prometheus-metrics`, `structural-refactoring` | move body under `## Requirements`, add `## Purpose` (one sentence derived from the requirements); drop the `ADDED` header |
| `config-builder` | add one `#### Scenario` to "Enrichment toggles with per-feature settings" restating its existing text as WHEN/THEN |
| `daily-forecast` | change "Only hourly variable count determines the API call weight" to "... SHALL determine ..." (wording only; the scenario already states the behavior) |

Deleting stubs removes their spec directory, which `openspec validate` would otherwise keep failing on. *Alternative:* keep stubs with a dummy requirement. Rejected: invents requirements to satisfy a tool.

### 6. Merging the stray root

`git mv src/openspec/changes/archive/2026-07-30-utc-everywhere openspec/changes/archive/`; create `openspec/specs/utc-timestamps/spec.md` from the change's delta using sync semantics (`## ADDED Requirements` becomes main-spec `## Purpose` + `## Requirements`); delete `src/openspec/` (its `config.yaml` is only `schema: spec-driven`). Verify with the guard script.

### 7. Config rules

Add to `openspec/config.yaml` `rules:`: `tasks`: "Chunks completable in one session, ordered by dependency." (FunkArr rule); `specs`: "Specs describe observable behavior; name classes/messages only at interface boundaries." Existing `proposal` and `tasks` rules stay. A `design` rule is not added (no clearly useful small rule). The archive practice ("never delete `openspec/changes/archive`") is documented in `AGENTS.md` Conventions once `restructure-agent-docs` has landed, not in `config.yaml`.

## Risks / Trade-offs

- [`--all` blocks PRs on future spec mistakes] → intended; the failure output names the spec.
- [Guard false positive on an unrelated `openspec` folder] → explicit exclusion list, extend by code change.
- [`npm install -g` in CI] → pinned version, `contents: read`, no secrets.
- [CLI behavior differs between 1.7.0 and newer] → single pinned version; bump deliberately.
- [Order dependency with `restructure-agent-docs`] → the `AGENTS.md` practice note is a task that runs only if `AGENTS.md` exists; otherwise it is a follow-up note.

## Migration Plan

Repairs and root merge first, then guard script, then workflow, in one branch. Validate locally before opening the PR. Rollback: revert the commit.
