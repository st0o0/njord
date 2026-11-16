# per-domain-options Specification

## Purpose
TBD - created by archiving change restructure-forecast-and-clients. Update Purpose after archive.
## Requirements
### Requirement: Independent forecast days per domain
AirQuality, Marine, Flood, Forecast, Ensemble, and Seasonal SHALL each have
their own configuration section with a forecast-days (or past-hours) setting. No domain's
forecast days SHALL affect another domain. All domain Options classes and their validators
SHALL live in `Njord.OpenMeteo/Configuration/`.

#### Scenario: Defaults
- **WHEN** no value is configured
- **THEN** Forecast SHALL use 4, Ensemble 4, AirQuality 5, Marine 8, and Flood 92 forecast days

#### Scenario: Forecast and Ensemble independent
- **WHEN** Forecast ForecastDays is 4 and Ensemble ForecastDays is 15
- **THEN** the Forecast API request SHALL use `forecast_days=4` and the Ensemble API request SHALL use `forecast_days=15`

#### Scenario: Options placement
- **WHEN** the solution is inspected
- **THEN** ForecastOptions, EnsembleOptions, and FloodOptions (plus their validators) SHALL be in `Njord.OpenMeteo/Configuration/`, alongside AirQualityOptions, MarineOptions, and SeasonalOptions

### Requirement: API limits enforced at startup
Each domain's forecast days SHALL be validated against its API limit when the service starts.

#### Scenario: Above limit
- **WHEN** Forecast is 17, Ensemble is 36, AirQuality is 8, Marine is 9, or Flood is 211
- **THEN** startup SHALL fail with a message naming the setting and its maximum

#### Scenario: At limit
- **WHEN** Forecast is 16, Ensemble is 35, AirQuality is 7, Marine is 8, and Flood is 210
- **THEN** validation SHALL succeed

#### Scenario: Below minimum
- **WHEN** a forecast-days value is 0 or negative
- **THEN** startup SHALL fail

### Requirement: SolarOptions owns panel orientation
`SolarOptions` (`Njord:Solar`) SHALL contain `Tilt` and `Azimuth`. Orientation is an API parameter and SHALL live with the domain options, not with a feature. See `solar-options` spec.

#### Scenario: Solar section defaults
- **WHEN** `IOptions<SolarOptions>.Value` is read without configuration
- **THEN** `Tilt` SHALL be 30 and `Azimuth` 180

### Requirement: ForecastOptions has no array defaults hook
`ForecastOptions` SHALL NOT implement `IHasArrayDefaults`. An empty `Models` array SHALL mean best_match.

#### Scenario: Empty models
- **WHEN** `Njord:Forecast:Models` is not configured
- **THEN** `Models` SHALL be empty and the Forecast request SHALL use best_match

#### Scenario: No interface
- **WHEN** `ForecastOptions` is inspected
- **THEN** it SHALL NOT implement `IHasArrayDefaults`

