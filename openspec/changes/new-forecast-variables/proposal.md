## Why

The Forecast API offers several high-value variables that njord does not request. Most notably, `thunderstorm_probability` is a direct probability from GFS NBM (National Blend of Models) that would dramatically improve the Thunderstorm alert -- currently njord derives thunderstorm risk from CAPE alone. `precipitation_type` from ECMWF classifies rain vs snow vs freezing rain vs ice pellets, improving winter alerts. `surface_temperature` enables better ice/frost detection. `snowfall_water_equivalent` provides more precise snow measurement than `snowfall` in cm.

## What Changes

- Add `thunderstorm_probability` to `ForecastApiVariable`/`ForecastParams`/`ForecastPoint` (GFS NBM only).
- Add `precipitation_type` to `ForecastApiVariable`/`ForecastParams`/`ForecastPoint` (ECMWF only).
- Add `surface_temperature` to `ForecastApiVariable`/`ForecastParams`/`ForecastPoint` (ECMWF only).
- Add `snowfall_water_equivalent` to `ForecastApiVariable`/`ForecastParams`/`ForecastPoint`.
- These are new params only -- no features consume them yet (features are a separate change).

## Non-goals

- Creating new feature rules that consume these variables (separate change per feature).
- Adding variables that are low-priority (lightning_density, albedo, roughness_length, sea_ice).
- Pressure level variables (separate change).
- Solar instant variants (low priority).

## Capabilities

### New Capabilities

- `thunderstorm-probability-var`: `thunderstorm_probability` as a requestable Forecast variable from GFS NBM.
- `precipitation-type-var`: `precipitation_type` as a requestable Forecast variable from ECMWF.

### Modified Capabilities

- `forecast-api-options`: Add `surface_temperature` and `snowfall_water_equivalent` to the Forecast variable set.

## Impact

- `src/Njord.OpenMeteo/Forecast/` -- ForecastApiVariable enum, response DTO
- `src/Njord.Core/Forecast/` -- ForecastParams, ForecastPoint
- Tests for all changed files

## Budget impact

No budget impact. These variables are only requested when a feature declares them in its requirements. Adding them to the enum and params does not trigger any API calls. When a future feature enables them, they add to the variable count in `CallWeight.Of` -- each variable adds ~0.1 call weight (1/10 of a call unit).
