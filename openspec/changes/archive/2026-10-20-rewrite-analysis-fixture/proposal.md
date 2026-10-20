## Why

`AnalysisFixture` (486 lines) in Njord.Tests.Shared is a god-fixture from the consensus era. It simulates multi-model forecasts, computes medians across models with per-parameter per-location hardcoded values, and builds `LocationConsensus` — none of which exists in the new architecture. Tests should build `DomainForecast<WeatherPoint>` directly with known values.

## What Changes

- Replace `AnalysisFixture` with a `WeatherForecastBuilder` that creates `DomainForecast<WeatherPoint>` with sensible defaults and fluent overrides
- Add `AirQualityForecastBuilder` and `FloodForecastBuilder` for the new domains
- Update all test files that reference `AnalysisFixture` to use the new builders
- Delete `AnalysisFixture.cs`

## Non-goals

- Changing test logic — only the fixture construction changes.

## Capabilities

### New Capabilities
- `test-fixture-redesign`: DomainForecast builders for all domains

### Modified Capabilities
_(none — test infrastructure only)_

## Impact

- **Njord.Tests.Shared**: AnalysisFixture replaced with domain-specific builders
- **All test projects using AnalysisFixture**: Updated to new builders
