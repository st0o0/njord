## Why

The Ensemble GFS model ID `gfs025_ensemble_mean` configured in `EnsembleOptions.DefaultModels` does not exist on the Open-Meteo Ensemble Mean API. The correct ID is `ncep_gefs025_ensemble_mean`. Every Ensemble request that includes GFS returns HTTP 400, breaking both `EnsembleModelComparison` and `ForecastModelComparison` features. Additionally, the Marine API now supports up to 16 forecast days (njord caps at 8) and offers `wave_peak_period` which njord does not request.

## What Changes

- **BREAKING** (config): Rename default Ensemble model from `gfs025_ensemble_mean` to `ncep_gefs025_ensemble_mean` in `EnsembleOptions.DefaultModels`. Existing user configs with the old ID will fail validation at startup.
- Add `WavePeakPeriod` to `MarineApiVariable`, `MarineParams`, and `MarinePoint`.
- Raise `MarineOptions.MaxForecastDays` from 8 to 16 and update the validator.
- Update CLAUDE.md, AGENTS.md, docs, and docker-compose references for the Marine max change.

## Non-goals

- Adding Google WeatherNext 2 as a third Ensemble model (separate change).
- Expanding Ensemble spread variables beyond the current 3 (separate change).
- Adding new Forecast API variables like `wind_speed_100m` (separate change).

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

- `ensemble-api-options`: Default model ID changes from `gfs025_ensemble_mean` to `ncep_gefs025_ensemble_mean`.
- `marine-expansion`: Add `wave_peak_period` variable and raise forecast days max from 8 to 16.

## Impact

- `src/Njord.OpenMeteo/Ensemble/EnsembleOptions.cs` -- default model ID fix
- `src/Njord.OpenMeteo/Marine/` -- MarineApiVariable, MarineClient, response mapping
- `src/Njord.Core/Marine/` -- MarineParams, MarinePoint
- `src/Njord.OpenMeteo/Marine/Configuration/` -- MarineOptions validator
- Tests for all changed files
- CLAUDE.md, AGENTS.md -- Marine `(8, max 8)` references
- `docs/data/features.json` and Config Builder if they reference Marine max days
- `docker-compose.example.yml` and `appsettings.Example.json` if they reference Marine config

## Budget impact

No polling change. The GFS model ID fix restores intended behavior (no new calls). The Marine forecast days max increase is opt-in (default stays 8). Adding `wave_peak_period` adds 1 variable to Marine requests -- negligible impact on call weight.
