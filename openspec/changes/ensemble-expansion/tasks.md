## 1. Add wind_speed_100m / wind_direction_100m to Forecast

- [ ] 1.1 Add `WindSpeed100m` and `WindDirection100m` to `ForecastParams` flags enum in `src/Njord.Core/Forecast/ForecastParams.cs`
- [ ] 1.2 Add `WindSpeed100m` and `WindDirection100m` to `ForecastPoint` in `src/Njord.Core/Forecast/ForecastPoint.cs`
- [ ] 1.3 Add `WindSpeed100m` and `WindDirection100m` members to `ForecastApiVariable` enum in `src/Njord.OpenMeteo/Forecast/` with correct `[JsonStringEnumMemberName]`
- [ ] 1.4 Add param-to-API-variable mapping in `ParamMappings` (or equivalent)
- [ ] 1.5 Add response field mapping to `ForecastPoint`
- [ ] 1.6 Add tests: param mapping, API variable name, response parsing

## 2. Expand EnsembleApiVariable and EnsembleParams

- [ ] 2.1 Add ~12 new spread members to `EnsembleApiVariable` enum: `pressure_msl_spread`, `cloud_cover_spread`, `wind_gusts_10m_spread`, `cape_spread`, `snowfall_spread`, `rain_spread`, `dew_point_2m_spread`, `relative_humidity_2m_spread`, `surface_pressure_spread`, `wind_direction_10m_spread`, `wet_bulb_temperature_2m_spread`, `wind_speed_100m_spread`
- [ ] 2.2 Add corresponding flags to `EnsembleParams` in `src/Njord.Core/Ensemble/EnsembleParams.cs`
- [ ] 2.3 Add corresponding nullable fields to `EnsemblePoint` in `src/Njord.Core/Ensemble/EnsemblePoint.cs`
- [ ] 2.4 Update Ensemble response parsing (`EnsembleResponseMappingExtensions`) to extract all new spread fields
- [ ] 2.5 Add param-to-API-variable mappings for the new spread variables
- [ ] 2.6 Add tests for all new mappings and parsing

## 3. Add WeatherNext as Third Default Model

- [ ] 3.1 Add `google_weathernext2_ensemble_mean` to `EnsembleOptions.DefaultModels` (as third entry)
- [ ] 3.2 Verify `EnsembleClient` handles 3 models correctly (multi-model comparison + spread extraction)
- [ ] 3.3 Update any tests that assert on the default model count or specific model IDs

## 4. Update Budget Calculations

- [ ] 4.1 Verify `DomainCallConfig.EnsembleModelCount` correctly resolves to 3 with the new defaults
- [ ] 4.2 Update `BudgetGuard` tests to expect 3-model cost calculations
- [ ] 4.3 Verify startup budget validation passes with 3 models and typical variable counts

## 5. Documentation and Config Updates

- [ ] 5.1 Update CLAUDE.md and AGENTS.md Ensemble defaults documentation (3 models, new spreads)
- [ ] 5.2 Update `docs/data/features.json` if it references Ensemble model count or spread variables
- [ ] 5.3 Update Config Builder budget calculation for 3 models
- [ ] 5.4 Update `docker-compose.example.yml` / `appsettings.Example.json` for new defaults

## 6. Validation

- [ ] 6.1 Run `dotnet build src/Njord.slnx`
- [ ] 6.2 Run `dotnet format src/Njord.slnx --verify-no-changes`
- [ ] 6.3 Run all test projects sequentially
