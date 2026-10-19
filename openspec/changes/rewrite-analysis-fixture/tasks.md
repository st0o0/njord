## 1. Create builders

- [x] 1.1 Add `WeatherForecastBuilder` to `src/Njord.Tests.Shared/` with fluent API and sensible defaults
- [x] 1.2 Add `AirQualityForecastBuilder` with AQ defaults (PM2.5=5, Ozone=40, etc.)
- [x] 1.3 Add `FloodForecastBuilder` with daily resolution and discharge defaults
- [x] 1.4 Add `LocationForecastBuilder` composing domain builders

## 2. Migrate tests

- [x] 2.1 Find all files referencing `AnalysisFixture` (`grep -r AnalysisFixture src/`)
- [x] 2.2 Migrate each to use the new builders
- [x] 2.3 Delete `src/Njord.Tests.Shared/AnalysisFixture.cs`

## 3. Validation

- [x] 3.1 `dotnet build Njord.slnx` — 0 errors
- [x] 3.2 All test projects pass
- [x] 3.3 `grep -r AnalysisFixture src/` returns nothing
