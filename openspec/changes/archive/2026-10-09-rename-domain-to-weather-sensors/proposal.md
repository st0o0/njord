## Why

`Njord.Domain` is a generic container name that hides which bounded context
(weather vs. sensors) a type belongs to, and "Domain" is already overloaded as
the architectural zone name (Ingest → Domain → Egress) in AGENTS.md. The
feature library `Njord.Sensors` is also misnamed relative to its own code:
`SensorHubActor`, `ISensorHubActor`, and the `"sensor-hub"` actor name already
describe it as the sensor hub, not a generic "sensors" library. Renaming both
makes the solution's bounded-context areas visible from project names alone,
matching the convention used in the sibling FunkArr repo (feature libraries
named directly by area, no generic "Domain" container).

## What Changes

- **BREAKING** (internal, pre-1.0): split `Njord.Domain` into two foundation
  libraries named by bounded context: `Njord.Weather` (was `Domain/Weather/`)
  and `Njord.Sensors` (was `Domain/Sensors/`). Both keep zero Njord
  dependencies, per the existing foundation-layer rule.
- **BREAKING** (internal, pre-1.0): rename the existing `Njord.Sensors`
  feature library (the `SensorHubActor` + gRPC ingestion code) to
  `Njord.SensorHub`.
- Split `Njord.Domain.Tests` into `Njord.Weather.Tests` and
  `Njord.Sensors.Tests` (mirroring the new foundation libraries); rename the
  existing `Njord.Sensors.Tests` (actor specs) to `Njord.SensorHub.Tests`.
- Update every `using Njord.Domain.Weather`/`Njord.Domain.Sensors` reference
  (~137 files) to `Njord.Weather`/`Njord.Sensors`, update all `.csproj`
  `ProjectReference`s and `Njord.slnx`, and update
  `Njord.Architecture.Tests` (`LayerReferenceSpec`, `ZoneArchitectureSpec`,
  `NjordArchitecture.BaseLibraryNames`) to assert the new project/namespace
  names instead of `Njord.Domain`.
- Update `AGENTS.md`'s solution-structure listing and layer-reference
  description to the new project names.
- No behavior change: this is a pure rename/restructure of existing types,
  actors, and tests. No message contracts, persistence DTOs, or MQTT/gRPC
  wire formats change.
- Fixes a pre-existing drift found while scoping this change:
  `Njord.Domain/Options/LocationOptions.cs` was never moved to
  `Njord.Core/Configuration/` during the earlier `domain-core-merge` change,
  even though that change's spec already required it ("Options types SHALL
  live in Njord.Core/Configuration"). Since `Njord.Domain` is being removed
  entirely, this file has to move somewhere — moving it to
  `Njord.Core/Configuration/LocationOptions.cs` finally satisfies that
  existing requirement instead of inventing a new home for it.

## Capabilities

### New Capabilities

(none — this restructures existing capabilities, it does not add new behavior)

### Modified Capabilities

- `architecture-zone-enforcement`: requirements that name `Njord.Domain.*`,
  `Njord.Sensors` (feature lib), and `BaseLibraryNames` update to the new
  `Njord.Weather`/`Njord.Sensors`/`Njord.SensorHub` names.
- `compute-library`: the "reference only `Njord.Domain`" requirement updates
  to "reference only `Njord.Weather` and `Njord.Sensors`" (Compute's Analysis
  types use both weather and sensor value objects).
- `domain-core-merge`: the requirement "`Njord.Domain` SHALL retain only
  Weather and Sensors" is removed — there is no `Njord.Domain` assembly left
  to constrain.
- `standalone-sub-options`: the reference to "`Njord.Sensors`'s setup
  container" updates to `Njord.SensorHub`'s setup container.
- `test-project-extraction`: the "Sensors specs live in
  `Njord.Sensors.Tests`" scenario updates to `Njord.SensorHub.Tests`.
- `test-project-structure`: the solution test-project list replaces
  `Njord.Domain.Tests` with `Njord.Weather.Tests` + `Njord.Sensors.Tests`, and
  `Njord.Sensors.Tests` (actor specs) with `Njord.SensorHub.Tests`.

## Non-goals

- Not touching `Njord.Core` or `Njord.Compute` naming — explicitly confirmed
  as fine as-is.
- Not fixing the pre-existing `Njord.Domain.Analysis` vs. actual
  `Njord.Compute.Analysis` drift in the `consensus-computation` and
  `daily-forecast-typing` specs — that inconsistency predates this change and
  is unrelated to the Weather/Sensors split.
- No change to any message contract, persistence DTO wire format, MQTT
  discovery/state payload, or gRPC proto — this is namespace/assembly
  restructuring only.
- No change to the `Njord.Domain`/`Njord.Core` zone *concept* in AGENTS.md's
  architecture guardrails (Ingest → Domain → Egress stays as the conceptual
  zone name); only the literal project named `Njord.Domain` disappears.

## Impact

- ~137 source and test files under `src/` referencing
  `Njord.Domain.Weather`/`Njord.Domain.Sensors` namespaces.
- Project/assembly renames: `Njord.Domain` → `Njord.Weather` + `Njord.Sensors`;
  `Njord.Sensors` (feature lib) → `Njord.SensorHub`.
- Test project renames/splits: `Njord.Domain.Tests` →
  `Njord.Weather.Tests` + `Njord.Sensors.Tests`; `Njord.Sensors.Tests` →
  `Njord.SensorHub.Tests`.
- `Njord.slnx`, all affected `.csproj` files, `Njord.Architecture.Tests`
  (`LayerReferenceSpec.cs`, `ZoneArchitectureSpec.cs`), and `AGENTS.md`.
- No API budget impact (no polling change).
