## Why

njord has a stray second OpenSpec root (`src/openspec/`, created by running the CLI from `src/`), six specs that fail `openspec validate`, and two dead spec stubs. There is also no quick local way to verify that `<repo root>/openspec` is the only root and that all specs are valid. CI automation of these checks is deliberately out of scope (see `openspec-ci-validation`).

## What Changes

- Merge the stray root: move `src/openspec/changes/archive/2026-07-30-utc-everywhere` to `openspec/changes/archive/`, sync its `utc-timestamps` delta into `openspec/specs/utc-timestamps/spec.md`, delete `src/openspec/`.
- Repair the six specs that fail validation today so `openspec validate --all` is green for specs:
  - delete the dead stubs `publisher-protocol` and `daily-consensus-aggregation` (all requirements removed, superseded by `egress-event` and `DailyConsensus`),
  - rewrite `prometheus-metrics` and `structural-refactoring` from unsynced delta format (`## ADDED Requirements`) into proper main-spec format with `## Purpose`,
  - add the missing scenario to a `config-builder` requirement,
  - add the missing `SHALL` to a `daily-forecast` requirement.
- Add local script `scripts/check-openspec-root.sh`: fails with an explicit message if any `openspec/` directory or OpenSpec config exists other than `<repo root>/openspec` (excluding `node_modules`, `.git`, `bin`, `obj`, `dist`).
- Document `openspec validate --all --no-interactive` and the script in `AGENTS.md` "Build & test".
- Extend `openspec/config.yaml` `rules:`: tasks rule "Chunks completable in one session, ordered by dependency" and one specs rule "Specs describe observable behavior; name classes/messages only at interface boundaries".
- Document in `AGENTS.md`: never delete `openspec/changes/archive`; the archive is tracked (no longer gitignored).

## Capabilities

### New Capabilities

None. Tooling and spec housekeeping; no runtime behavior changes. The change sets `skip_specs: true`. Direct edits to main specs are format repairs, not requirement changes (the one wording fix is called out in design.md).

### Modified Capabilities

None.

## Impact

- New: `scripts/check-openspec-root.sh`.
- Edited: `AGENTS.md`, `openspec/config.yaml` (`rules:` only), `openspec/specs/{prometheus-metrics,structural-refactoring,config-builder,daily-forecast}/spec.md`, `openspec/specs/utc-timestamps/spec.md` (new).
- Removed: `src/openspec/`, `openspec/specs/{publisher-protocol,daily-consensus-aggregation}/`.
- API budget: none, no polling added or altered (0 requests/month against the 300k free-tier limit).
- No code under `src/Njord/` or `src/Njord.Tests/`, and nothing under `.github/`.

## Non-goals

- Any CI or workflow change (moved to `openspec-ci-validation`, blocked pending design discussion).
- Consolidating overlapping specs (consensus family, `daily-forecast-*`, `health-*`, `grpc-forecast-*`).
- Changes to FunkArr.
- Restoring the ~22 archived changes deleted in commit `19f0439`.
- Changing requirement semantics of any spec.
