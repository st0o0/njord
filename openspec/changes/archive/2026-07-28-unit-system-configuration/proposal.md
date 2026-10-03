## Why

njord delivers all forecast values in fixed metric units (°C, m/s, hPa, mm). Clients like ha-njord have no way to know which units are in use and must hardcode assumptions. Users in Imperial-unit regions get no native support — HA can convert at display time, but `native_unit_of_measurement` is wrong, precision suffers, and the catalog provides no unit metadata for dynamic mapping.

## What Changes

- **Unit system config option**: New `UnitSystem` setting (Metric | Imperial) in `NjordOptions`, defaulting to Metric. Determines the units njord delivers values in across all egress paths.
- **Open-Meteo query parameter adaptation**: `OpenMeteoClient` sets `temperature_unit`, `wind_speed_unit`, and `precipitation_unit` based on the configured unit system, so most values arrive from the API already in the target unit.
- **Server-side conversion for API gaps**: Open-Meteo has no unit options for pressure (hPa), snowfall (cm), snow_depth (m), visibility (m), and freezing_level_height (m). A thin converter handles these ~6 parameters when Imperial is selected.
- **ParameterMeta in GetCatalogResponse**: New `repeated ParameterMeta parameters` field exposing each active parameter's name and unit, so clients can dynamically set `native_unit_of_measurement`.
- **Enrichment normalization**: Values are normalized back to metric before entering the enrichment pipeline, so all thresholds (frost < 0 °C, storm gusts > 20 m/s, etc.) remain unchanged.
- **MQTT Discovery adaptation**: `unit_of_measurement` in discovery payloads reflects the active unit system.
- **ParameterDef.Unit becomes unit-system-aware**: Instead of hardcoded metric strings, `ParameterDef` resolves its unit based on the active `UnitSystem`.

## Non-goals

- **Custom per-parameter unit selection** — only Metric and Imperial as presets.
- **Runtime unit system switching via SetSettings** — config-file only for now; runtime mutation is a follow-up.
- **device_class in ParameterMeta** — ha-njord keeps its own HA device class mapping.
- **Enrichment output conversion** — enrichment results (indices, alerts, trends) stay in their current units; only raw forecast data is unit-aware.

## Capabilities

### New Capabilities

- `unit-system`: Unit system configuration, parameter unit resolution, and server-side conversion for parameters without Open-Meteo API unit support.
- `catalog-parameter-metadata`: ParameterMeta message in GetCatalogResponse exposing active parameter names and units.

### Modified Capabilities

- `openmeteo-client`: Query parameters adapt to configured unit system (temperature_unit, wind_speed_unit, precipitation_unit).
- `service-configuration`: New `UnitSystem` option in NjordOptions.
- `grpc-v2-weather-service`: GetCatalogResponse gains `repeated ParameterMeta parameters` field.
- `mqtt-egress`: Discovery payloads use unit from active unit system instead of hardcoded metric.

## Impact

- **Proto**: `common.proto` gains `ParameterMeta` message; `weather.proto` gains field 3 on `GetCatalogResponse`.
- **Domain**: `ParameterDef.Unit` becomes dynamic; new `UnitSystem` enum and `UnitConverter` in Domain layer.
- **Ingest**: `OpenMeteoClient` URL builder adds unit query params; unit validation in response parsing adapts.
- **Egress**: `DiscoveryPayloadBuilder` reads unit from config-aware source instead of `ParameterDef.Unit` directly.
- **Enrichment**: Thin normalization layer before enrichment input — no changes to enrichment logic itself.
- **Config**: `NjordOptions` gains `UnitSystem` property; validator checks valid enum value.
- **API budget**: No change — same number of requests, same variables. Unit params don't affect call weight.
