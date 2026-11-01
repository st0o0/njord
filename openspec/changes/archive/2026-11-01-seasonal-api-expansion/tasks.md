## 1. Expand Monthly Seasonal Variables

- [x] 1.1 Add ~10 new members to `SeasonalApiVariable` enum: `temperature_2m`, `snowfall`, `snow_depth`, `cloud_cover`, `wind_speed_10m`, `pressure_msl`, `sunshine_duration`, `shortwave_radiation`, `wind_gusts_10m`, `sea_surface_temperature`
- [x] 1.2 Add corresponding flags to `SeasonalParams` in `src/Njord.Core/Seasonal/SeasonalParams.cs`
- [x] 1.3 Add corresponding fields to `SeasonalPoint` in `src/Njord.Core/Seasonal/SeasonalPoint.cs`
- [x] 1.4 Add param-to-API-variable mappings
- [x] 1.5 Add response fields to Seasonal response DTO and map to `SeasonalPoint`
- [x] 1.6 Add tests for all new mappings and parsing

## 2. Add Weekly Resolution Support

- [x] 2.1 Define `SeasonalWeeklyParams` flags enum for EFI/SOT/anomaly threshold variables
- [x] 2.2 Define `SeasonalWeeklyPoint` record for weekly data
- [x] 2.3 Add weekly API variable enum members for EFI, SOT, and anomaly thresholds
- [x] 2.4 Update `SeasonalClient` to optionally make a second request with `&weekly=` variables
- [x] 2.5 Add weekly response parsing
- [x] 2.6 Add tests for weekly request construction and parsing

## 3. Configure Explicit Seasonal Model

- [x] 3.1 Add `Model` or `Models` property to `SeasonalOptions` with default `ecmwf_seasonal_ensemble_mean_seamless`
- [x] 3.2 Update `SeasonalClient` to pass `&models=` parameter
- [x] 3.3 Add tests for model parameter in request URL

## 4. Raise ForecastMonths Max

- [x] 4.1 Update `SeasonalOptions` max from 6 to 7
- [x] 4.2 Update validator to accept 7 and reject 8
- [x] 4.3 Update CLAUDE.md/AGENTS.md references from `(3, 1..6)` to `(3, 1..7)`

## 5. Documentation

- [x] 5.1 Update `docs/data/features.json` for new Seasonal variables
- [x] 5.2 Update Config Builder for Seasonal model and new variables
- [x] 5.3 Update CLAUDE.md/AGENTS.md Seasonal documentation

## 6. Validation

- [x] 6.1 Run `dotnet build src/Njord.slnx`
- [x] 6.2 Run `dotnet format src/Njord.slnx --verify-no-changes`
- [x] 6.3 Run all test projects sequentially
