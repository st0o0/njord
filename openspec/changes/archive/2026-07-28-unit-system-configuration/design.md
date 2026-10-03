## Context

njord delivers all values in fixed metric units. The Open-Meteo API supports unit selection via query parameters (`temperature_unit`, `wind_speed_unit`, `precipitation_unit`) but njord hardcodes `wind_speed_unit=ms` and leaves the rest at defaults. Clients have no way to discover which units are active — they must hardcode assumptions.

The `ParameterDef` record carries a `Unit` string (e.g. `"°C"`, `"m/s"`) that is baked into the registry at compile time and consumed by MQTT discovery payloads and the gRPC service.

## Goals / Non-Goals

**Goals:**

- Let the user choose between Metric and Imperial unit systems via configuration
- Have Open-Meteo deliver values in the target units wherever it supports unit parameters
- Convert the small set of parameters lacking API-side unit support server-side
- Expose active parameter units in the gRPC catalog so clients can adapt dynamically
- Keep all enrichment logic in metric (normalize at the enrichment boundary)
- Update MQTT discovery payloads to reflect the active unit system

**Non-Goals:**

- Custom per-parameter unit selection
- Runtime unit system switching via AdminService (follow-up)
- Exposing device_class in ParameterMeta

## Decisions

### D1: Unit system as an enum, not a string

`UnitSystem` is a C# enum (`Metric | Imperial`) rather than a freeform string. This prevents invalid states and makes exhaustive pattern matching possible. If Custom is ever needed, it becomes a third variant with per-parameter overrides — but that's out of scope.

**Alternative considered:** Freeform string — rejected because it pushes validation to every consumer.

### D2: ParameterDef.Unit stays static; unit resolution is a separate function

Changing `ParameterDef.Unit` to be mutable or unit-system-aware would break its use as a dictionary key and complicate the registry. Instead, a pure function `UnitResolver.GetUnit(ParameterDef, UnitSystem)` returns the active unit string. The registry remains the single source of metric units; the resolver maps them.

**Alternative considered:** Making `ParameterDef` carry both metric and imperial units — rejected because it couples every parameter definition to the imperial mapping, and the resolver approach is more extensible.

### D3: Open-Meteo query params as the primary conversion mechanism

For parameters where Open-Meteo supports unit selection (temperature, wind speed, precipitation), we set the API query parameters to match the configured unit system. This means values arrive already in the target unit — no server-side conversion needed for ~50 of ~60 parameters.

The query parameter mapping:
- Metric: `temperature_unit=celsius`, `wind_speed_unit=ms`, `precipitation_unit=mm`
- Imperial: `temperature_unit=fahrenheit`, `wind_speed_unit=mph`, `precipitation_unit=inch`

### D4: Server-side conversion for the API gaps

Six parameter families lack Open-Meteo unit selection:

| Parameter(s) | Metric | Imperial | Conversion |
|---|---|---|---|
| pressure_msl, surface_pressure | hPa | inHg | × 0.02953 |
| snowfall, snowfall_sum | cm | in | × 0.3937 |
| snow_depth | m | in | × 39.37 |
| visibility | m | ft | × 3.28084 |
| freezing_level_height | m | ft | × 3.28084 |

A static `UnitConverter` class applies these conversions after deserialization, before the values enter the domain model. The converter is a pure function: `double Convert(ParameterDef param, double value, UnitSystem system)`. For Metric, it's a no-op.

### D5: Enrichment normalization at the pipeline boundary

Enrichment features (alerts, trends, indices, energy, consensus, derived) all use hardcoded metric thresholds. Rather than maintaining dual threshold sets, the enrichment pipeline receives a normalization step: when the unit system is Imperial, forecast values are converted back to metric before entering enrichment. This is the inverse of D4 + the API unit params — effectively undoing the unit conversion for the enrichment path only.

The normalization lives in a single method that the enrichment actor calls before dispatching to features. Enrichment output stays in its current form (indices are dimensionless scores, alerts reference thresholds, etc.).

### D6: ParameterMeta as a flat list in GetCatalogResponse

`GetCatalogResponse` gains `repeated ParameterMeta parameters` (field 3). Each entry carries `name` (the API name, e.g. `"temperature_2m"`) and `unit` (the active unit, e.g. `"°C"` or `"°F"`). The list is derived from the resolved parameter set, so it only includes parameters the user has configured.

No granularity field — clients can infer hourly vs. daily from the field names or the forecast messages. No device_class — clients maintain their own HA mapping.

### D7: OpenMeteoClient unit verification adapts to configured units

The existing unit verification in `OpenMeteoClient` checks that `hourly_units` match expected values (e.g. temperature must be `°C`). With configurable units, the expected values change. The verification must use the `UnitResolver` to determine what units to expect from the API response.

### D8: MQTT discovery reads units from UnitResolver

`DiscoveryPayloadBuilder` currently reads `ParameterDef.Unit` directly for `unit_of_measurement`. It will instead call `UnitResolver.GetUnit(param, unitSystem)` to get the active unit. The derived device units (wind_chill in °C, decay_rate in °C/h) also adapt via the resolver.

## Risks / Trade-offs

**[Risk] Enrichment normalization adds a conversion round-trip** → The overhead is negligible (a few multiplications per forecast point per cycle). The alternative — dual thresholds — would be error-prone and hard to maintain.

**[Risk] Unit verification false positives during transition** → If Open-Meteo changes its default behavior for a unit parameter, the verification might reject valid responses. Mitigation: the verification already compares against a dynamic expected-unit map, not hardcoded strings.

**[Risk] Snowfall unit gap may close** → Open-Meteo might add a snowfall unit parameter in the future. If they do, we can remove the server-side conversion for snowfall and let the API handle it. The converter is designed to be easy to shrink.

**[Trade-off] ParameterDef.Unit becomes "metric unit" only** → Its value is still useful as the canonical/storage unit but no longer represents the egress unit. Code that previously read `param.Unit` for display purposes must use `UnitResolver` instead.
