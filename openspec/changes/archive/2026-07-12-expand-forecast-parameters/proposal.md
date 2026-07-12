## Why

Open-Meteo's `/v1/forecast` endpoint exposes ~50 hourly and ~20 daily variables, but njord hardcodes exactly 9 hourly parameters. Users who want wind direction, snow depth, visibility, UV index, soil data, or solar radiation must wait for code changes per variable. The service should expose the full forecast capability of its upstream API, selectable via configuration.

## What Changes

- **BREAKING**: Replace the closed `WeatherParameter` enum and typed `ForecastPoint` record with a registry-driven, dictionary-based parameter model that supports all ~50 hourly and ~20 daily Open-Meteo forecast variables.
- **BREAKING**: Replace the static 9-variable query string in `OpenMeteoClient` with a dynamic variable list derived from the active configuration.
- Introduce a **parameter group** configuration model with three groups: Weather (default), Solar, Soil — plus `Extra`/`Exclude` overrides for individual variables.
- Extend the HA entity grid to include daily forecast sensors (today, +1d, +2d, +3d based on `forecast_days`) alongside the existing hourly horizon sensors.
- Update the budget validator to account for API call weight (`ceil(hourly_vars / 10)`), replacing the current assumption of weight 1.0.
- **BREAKING**: DTO deserialization becomes dynamic (variable list not known at compile time).
- Prepare an endpoint extensibility seam (`ForecastSource`) for future non-forecast APIs (air quality, marine) without implementing them.

## Non-goals

- Implementing Air Quality, Marine, Flood, or other non-forecast endpoints (prepared structurally, not activated).
- A configuration UI (Vue.js frontend is a separate future change).
- Consensus forecast computation (deferred per prior decision).
- Minutely-15 sub-hourly data.

## Capabilities

### New Capabilities
- `parameter-registry`: Static catalog of all known Open-Meteo forecast variables with metadata (API name, unit, HA device class, group, hourly/daily) and group-based resolution logic.
- `daily-forecast`: Daily aggregate variables as HA sensors with day-offset horizons, separate from the hourly grid.

### Modified Capabilities
- `openmeteo-client`: Variable list becomes dynamic (config-driven, not hardcoded); response deserialization handles an arbitrary set of hourly + daily arrays; unit verification adapts to the active parameter set.
- `weather-domain`: Parameter set moves from a closed enum to a registry-backed open model; `ForecastPoint` becomes dictionary-based; `ForecastSeries` carries both hourly points and daily aggregates.
- `service-configuration`: New `Parameters` config section (Groups/Extra/Exclude); budget projection incorporates call weight from variable count.
- `mqtt-egress`: Discovery payloads and state topics adapt dynamically to the active parameter set; daily sensors added to the device/entity scheme.

## Impact

- **Domain layer**: `WeatherParameter`, `ForecastPoint`, `WeatherParameterExtensions` replaced entirely.
- **Ingest layer**: `OpenMeteoClient`, `OpenMeteoDtos`, `OpenMeteoJsonContext` rewritten for dynamic variable sets.
- **Egress layer**: `DiscoveryPayloadBuilder`, `StatePayloadBuilder`, `ParameterKeys`, `TopicScheme` adapt to registry-driven parameters.
- **Configuration**: `NjordOptions`, `NjordOptionsValidator` extended with parameter group config and weight-aware budget checks.
- **Tests**: All existing parameter-specific tests rewritten against the new model.
- **API budget**: Weather group default (~30 hourly vars) increases call weight from 1.0 to 3.0; projected monthly usage for 1 location / 8 models / 60 min interval rises from ~5,760 to ~17,280 effective calls — well within the 300k free-tier budget.
