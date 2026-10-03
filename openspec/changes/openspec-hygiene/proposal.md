## Why

Nothing in CI checks OpenSpec artifacts or where they live. njord already has a stray second root (`src/openspec/`, created by running the CLI from `src/`), six specs that fail `openspec validate`, and two dead spec stubs. A root guard plus validation in CI keeps `<repo root>/openspec` the only root and keeps specs valid. FunkArr is getting the same guard in its own change (`openspec-ci-validation`); njord mirrors its design so both repos behave identically.

## What Changes

- Add `.github/scripts/check-openspec-root.sh`: fails with an explicit message if any `openspec/` directory or OpenSpec config exists anywhere other than `<repo root>/openspec` (excluding `node_modules`, `.git`, `bin`, `obj`, `dist`). Runnable locally. Same file content as FunkArr's.
- Add `.github/workflows/openspec.yml`: `pull_request` + `workflow_dispatch`, `contents: read`, installs `@fission-ai/openspec@1.7.0`, runs the guard and `openspec validate --all --no-interactive` (blocking).
- Merge the stray root: move `src/openspec/changes/archive/2026-07-30-utc-everywhere` to `openspec/changes/archive/`, sync its `utc-timestamps` delta into `openspec/specs/utc-timestamps/spec.md`, delete `src/openspec/`.
- Repair the six specs that fail validation today so `--all` can be blocking from day one:
  - delete the dead stubs `publisher-protocol` and `daily-consensus-aggregation` (all requirements removed, superseded by `egress-event` and `DailyConsensus`),
  - rewrite `prometheus-metrics` and `structural-refactoring` from unsynced delta format (`## ADDED Requirements`) into proper main-spec format with `## Purpose`,
  - add the missing scenario to a `config-builder` requirement,
  - add the missing `SHALL` to a `daily-forecast` requirement.
- Extend `openspec/config.yaml` `rules:`: tasks rule "Chunks completable in one session, ordered by dependency" (adopted from FunkArr) and one specs rule "Specs describe observable behavior; name classes/messages only at interface boundaries".
- Document the practice: never delete `openspec/changes/archive` (history of decisions stays in the tree).

## Capabilities

### New Capabilities

None. CI, tooling and spec housekeeping; no runtime behavior changes. The change sets `skip_specs: true`. Direct edits to main specs are format repairs, not requirement changes (the one wording fix is called out in design.md).

### Modified Capabilities

None.

## Impact

- New files: `.github/workflows/openspec.yml`, `.github/scripts/check-openspec-root.sh`.
- Edited: `openspec/config.yaml` (`rules:` only), `openspec/specs/{prometheus-metrics,structural-refactoring,config-builder,daily-forecast}/spec.md`, `openspec/specs/utc-timestamps/spec.md` (new or merged).
- Removed: `src/openspec/`, `openspec/specs/{publisher-protocol,daily-consensus-aggregation}/`.
- API budget: none, no polling added or altered (0 requests/month against the 300k free-tier limit).
- No code under `src/Njord/` or `src/Njord.Tests/` changes.

## Non-goals

- Consolidating overlapping specs (consensus family, `daily-forecast-*`, `health-*`, `grpc-forecast-*`).
- Changes to FunkArr (it has its own change, and its `config.yaml` is not touched).
- Restoring the ~22 archived changes deleted in commit `19f0439`.
- Adding `dotnet format --verify-no-changes` to CI.
- Changing requirement semantics of any spec.
