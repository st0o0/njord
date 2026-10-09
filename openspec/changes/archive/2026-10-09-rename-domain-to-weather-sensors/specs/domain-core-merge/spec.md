## REMOVED Requirements

### Requirement: Domain SHALL retain only Weather and Sensors

**Reason**: `Njord.Domain` no longer exists as an assembly — it is split into
`Njord.Weather` and `Njord.Sensors`, each a standalone foundation library
named by bounded context (see `rename-domain-to-weather-sensors`). A
requirement constraining the contents of `Njord.Domain` is meaningless once
that project is gone.
**Migration**: The guarantee this requirement protected (no `Analysis/` or
`Options/` leaking back into the weather/sensor value-object libraries) is
now implicit in the split itself: `Njord.Weather` contains only the former
`Domain/Weather/` types and `Njord.Sensors` contains only the former
`Domain/Sensors/` types, enforced by `architecture-zone-enforcement`'s
layer-reference-direction requirement.

`Njord.Domain` SHALL contain only `Weather/` and `Sensors/` folders — the pure
record types referenced by `Njord.Messages`.

#### Scenario: Domain contains no Analysis or Options
- **WHEN** listing files in `Njord.Domain/`
- **THEN** only `Weather/` and `Sensors/` subdirectories exist

### Requirement: Options types SHALL live in Njord.Core/Configuration

**Reason**: This requirement was never actually satisfied —
`Njord.Domain/Options/LocationOptions.cs` was never moved. Implementing the
Weather/Sensors split surfaced why: `Njord.Messages/Pipeline/WeightedTarget.cs`
uses `LocationOptions` directly, and `Njord.Messages` must not depend on
`Njord.Core` (the reference direction is Weather/Sensors <- Messages <- Core,
never the reverse). Moving `LocationOptions` to `Njord.Core/Configuration`
as this requirement demanded would have created a layering violation.
**Migration**: `LocationOptions` moves to `Njord.Weather` instead (it is the
weather-polling location config: name, coordinates, which models to poll —
a natural fit for the Weather foundation library, and reachable from
`Njord.Messages` without breaking the reference direction). Every other
consumer (`Njord.Core`, `Njord.Grpc`, `Njord.Ingest`, `Njord.Enrichment`,
feature-library tests) already references `Njord.Weather` or gains that
reference as part of this change.

All types previously in `Njord.Domain/Options/` SHALL reside in
`Njord.Core/Configuration/`. They already use `Njord.Configuration` namespace.

#### Scenario: Options types merge cleanly
- **WHEN** inspecting `LocationOptions.cs` in `Njord.Core/Configuration/`
- **THEN** its namespace is `Njord.Configuration` (unchanged)
