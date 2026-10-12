## Why

A full project review after the gateway-pipeline-redesign refactoring revealed 17
findings across architecture tests, persistence conventions, metrics naming, test
conventions, test coverage, and leftover artifacts. None are bugs — the runtime
behavior is correct — but they represent convention drift, latent gaps in
architectural enforcement, and post-refactoring debris that should be resolved
before the next feature wave.

## What Changes

- **Architecture tests**: add Pipeline to zone-level lateral-independence check
  with a custom variant (Pipeline legitimately imports analysis libs but must not
  import Mqtt/Grpc/OpenMeteo/Budget/SensorHub)
- **Persistence records**: **BREAKING** rename DTOs to FunkArr naming
  (`BudgetTrackerSnapshotDto` → `PersistedBudgetTrackerState`, etc.), restructure
  `Njord.Persistence/` to `Events/<Domain>/` layout, add missing roundtrip and
  Verify wire-format tests
- **Metrics**: fix 3 metric names missing the `<area>` segment, remove dead
  `ThrottleWait` histogram
- **Test assertions**: fix `Assert.True(x == y)`, vacuous `Assert.NotNull`,
  `Assert.Single`-then-`[0]`, and reduce `!.` on nullable results
- **Test coverage**: add Indices tests exercising `OverrideIndoorTemp` with real
  sensor data, identify and stabilize the flaky Core.Tests test
- **Cleanup**: delete ghost directories `Njord.Ingest/` and `Njord.Ingest.Tests/`,
  align `Akka.Persistence.Sql.Hosting` version with rest of stack

## Non-goals

- No runtime behavior changes — this is purely convention alignment, test
  hardening, and cleanup
- No new features or capabilities
- No API-budget impact (no polling changes)

## Capabilities

### New Capabilities

- `persistence-funkarr-alignment`: Rename persistence records to FunkArr naming
  conventions, restructure project layout, add wire-format tests
- `pipeline-zone-enforcement`: Add Pipeline to architecture zone tests with
  orchestrator-aware variant
- `metrics-naming-hygiene`: Fix metric names to follow `njord_<area>_<name>`
  convention, remove dead instruments
- `test-assertion-hygiene-2`: Fix remaining assertion convention violations
  across test projects
- `test-coverage-gaps`: Add missing Indices sensor-override tests and stabilize
  flaky test
- `post-refactor-cleanup`: Delete ghost directories and align package versions

### Modified Capabilities

(none — no existing spec-level requirements are changing)

## Impact

- `Njord.Persistence/` — record renames and file moves (breaking: type names
  change, `[JsonProperty]` strings stay stable so wire format is preserved)
- `Njord.Budget/`, `Njord.Pipeline/` — update imports after persistence renames
- `Njord.OpenMeteo/Telemetry.cs`, `Njord.Pipeline/Telemetry.cs` — metric name
  changes (dashboards referencing old names need updating)
- `Njord.Architecture.Tests/` — new zone test for Pipeline
- `Njord.Persistence.Tests/` — new roundtrip and Verify tests
- `Njord.Indices.Tests/` — new sensor-override tests
- `Njord.Core.Tests/` — flaky test fix
- Multiple `*Spec.cs` files — assertion convention fixes
- `Directory.Packages.props` — version bump for Akka.Persistence.Sql.Hosting
