## 1. Fix Ensemble GFS Model ID

- [x] 1.1 Change `DefaultModels` in `src/Njord.OpenMeteo/Ensemble/EnsembleOptions.cs` from `gfs025_ensemble_mean` to `ncep_gefs025_ensemble_mean`
- [x] 1.2 Update any tests in `src/Njord.OpenMeteo.Tests/` that reference the old model ID
- [x] 1.3 Update `docker-compose.example.yml` and `appsettings.Example.json` if they reference the old model ID
- [x] 1.4 Update CLAUDE.md and AGENTS.md references to the GFS model ID

## 2. Add Marine wave_peak_period

- [x] 2.1 Add `WavePeakPeriod` flag to `MarineParams` in `src/Njord.Core/Marine/MarineParams.cs`
- [x] 2.2 Add `WavePeakPeriod` to `MarinePoint` in `src/Njord.Core/Marine/MarinePoint.cs`
- [x] 2.3 Add `WavePeakPeriod` member to `MarineApiVariable` enum in `src/Njord.OpenMeteo/Marine/` with `[JsonStringEnumMemberName("wave_peak_period")]`
- [x] 2.4 Add mapping in `MarineParamMappingExtensions` (or equivalent) from `MarineParams.WavePeakPeriod` to `MarineApiVariable.WavePeakPeriod`
- [x] 2.5 Add `wave_peak_period` field to the Marine response DTO and map to `MarinePoint.WavePeakPeriod`
- [x] 2.6 Add tests for the new variable: param mapping, API variable name, response parsing

## 3. Raise Marine ForecastDays Max

- [x] 3.1 Update `MaxForecastDays` constant in `src/Njord.OpenMeteo/Marine/Configuration/MarineOptions.cs` (or validator) from 8 to 16
- [x] 3.2 Update validator tests to accept 16 and reject 17
- [x] 3.3 Update CLAUDE.md and AGENTS.md references from `(8, max 8)` to `(8, max 16)`

## 4. Documentation and Config Updates

- [x] 4.1 Update `docs/data/features.json` if it references Marine max days
- [x] 4.2 Update Config Builder (`docs/.vitepress/theme/builder/`) if it references Marine max days

## 5. Validation

- [x] 5.1 Run `dotnet build src/Njord.slnx`
- [x] 5.2 Run `dotnet format src/Njord.slnx --verify-no-changes`
- [x] 5.3 Run all test projects sequentially
