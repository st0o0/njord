## 1. Expand Monthly Seasonal Variables

- [ ] 1.1 Add ~10 new members to `SeasonalApiVariable` enum: `temperature_2m`, `snowfall`, `snow_depth`, `cloud_cover`, `wind_speed_10m`, `pressure_msl`, `sunshine_duration`, `shortwave_radiation`, `wind_gusts_10m`, `sea_surface_temperature`
- [ ] 1.2 Add corresponding flags to `SeasonalParams` in `src/Njord.Core/Seasonal/SeasonalParams.cs`
- [ ] 1.3 Add corresponding fields to `SeasonalPoint` in `src/Njord.Core/Seasonal/SeasonalPoint.cs`
- [ ] 1.4 Add param-to-API-variable mappings
- [ ] 1.5 Add response fields to Seasonal response DTO and map to `SeasonalPoint`
- [ ] 1.6 Add tests for all new mappings and parsing

## 2. Add Weekly Resolution Support

- [ ] 2.1 Define `SeasonalWeeklyParams` flags enum for EFI/SOT/anomaly threshold variables
- [ ] 2.2 Define `SeasonalWeeklyPoint` record for weekly data
- [ ] 2.3 Add weekly API variable enum members for EFI, SOT, and anomaly thresholds
- [ ] 2.4 Update `SeasonalClient` to optionally make a second request with `&weekly=` variables
- [ ] 2.5 Add weekly response parsing
- [ ] 2.6 Add tests for weekly request construction and parsing

## 3. Configure Explicit Seasonal Model

- [ ] 3.1 Add `Model` or `Models` property to `SeasonalOptions` with default `ecmwf_seasonal_ensemble_mean_seamless`
- [ ] 3.2 Update `SeasonalClient` to pass `&models=` parameter
- [ ] 3.3 Add tests for model parameter in request URL

## 4. Raise ForecastMonths Max

- [ ] 4.1 Update `SeasonalOptions` max from 6 to 7
- [ ] 4.2 Update validator to accept 7 and reject 8
- [ ] 4.3 Update CLAUDE.md/AGENTS.md references from `(3, 1..6)` to `(3, 1..7)`

## 5. Documentation

- [ ] 5.1 Update `docs/data/features.json` for new Seasonal variables
- [ ] 5.2 Update Config Builder for Seasonal model and new variables
- [ ] 5.3 Update CLAUDE.md/AGENTS.md Seasonal documentation

## 6. Validation

- [ ] 6.1 Run `dotnet build src/Njord.slnx`
- [ ] 6.2 Run `dotnet format src/Njord.slnx --verify-no-changes`
- [ ] 6.3 Run all test projects sequentially
