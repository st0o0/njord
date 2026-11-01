## Why

The Seasonal API offers 35+ monthly variables but njord only requests 3 (`temperature_2m_anomaly`, `precipitation_anomaly`, `precipitation_mean`). The API also has a weekly resolution with unique probabilistic variables (Extreme Forecast Index, Shift of Tails, anomaly threshold probabilities) that are not available on any other API -- these are specifically designed for long-range risk assessment. Additionally, njord does not specify a model, missing the newer EC46 model which has more variables than SEAS5, and the seamless blend of both.

## What Changes

- Add ~10 high-value monthly variables to `SeasonalApiVariable`/`SeasonalParams`/`SeasonalPoint`: `temperature_2m` (absolute), `snowfall`, `snow_depth`, `cloud_cover`, `wind_speed_10m`, `pressure_msl`, `sea_surface_temperature`, `sunshine_duration`, `shortwave_radiation`, `wind_gusts_10m`.
- Add weekly resolution support with EFI/Shift of Tails variables as a new data tier.
- Explicitly configure `ecmwf_seasonal_ensemble_mean_seamless` as the default model (EC46+SEAS5 blend).
- Expand `SeasonalOptions.ForecastMonths` max from 6 to 7 (API supports 217 days).

## Non-goals

- Hourly or daily Seasonal resolution (out of scope -- monthly and weekly cover the use case).
- Creating feature rules that consume the new variables (separate changes per feature).
- Soil temperature/moisture at seasonal scale (niche, can be added later).
- Heat/energy flux variables (latent_heat_flux, sensible_heat_flux -- research-grade).

## Capabilities

### New Capabilities

- `seasonal-weekly-probabilistic`: Weekly EFI, Shift of Tails, and anomaly probability threshold variables from the Seasonal API.

### Modified Capabilities

- `seasonal-outlook`: Expand monthly variables from 3 to ~13, add explicit model selection, raise forecast months max.

## Impact

- `src/Njord.OpenMeteo/Seasonal/` -- SeasonalApiVariable, SeasonalClient, response mapping
- `src/Njord.Core/Seasonal/` -- SeasonalParams, SeasonalPoint
- `src/Njord.OpenMeteo/Seasonal/Configuration/` -- SeasonalOptions
- Tests for all changed files
- CLAUDE.md/AGENTS.md -- Seasonal documentation
- `docs/data/features.json` -- Seasonal variable documentation

## Budget impact

Seasonal is already counted as 30 requests/month/location in BudgetGuard (fixed, not per-poll). Adding more monthly variables does not change the call count. Adding weekly resolution adds a second Seasonal request per poll cycle -- but at ~daily poll interval this is ~30 more calls/month/location. With 2 locations: 60 additional calls/month. Negligible against the 300k limit.
