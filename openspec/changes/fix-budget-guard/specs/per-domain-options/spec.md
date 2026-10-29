## MODIFIED Requirements

### Requirement: Independent forecast days per domain
AirQuality, Marine, Flood, Forecast, Ensemble, Seasonal, and Satellite SHALL each have
their own configuration section with a forecast-days (or past-hours) setting. No domain's
forecast days SHALL affect another domain. All domain Options classes and their validators
SHALL live in `Njord.OpenMeteo/Configuration/`.

#### Scenario: Defaults
- **WHEN** no value is configured
- **THEN** Forecast SHALL use 4, Ensemble 4, AirQuality 5, Marine 8, and Flood 92 forecast days

#### Scenario: Forecast and Ensemble independent
- **WHEN** Forecast ForecastDays is 4 and Ensemble ForecastDays is 15
- **THEN** the Forecast API request SHALL use `forecast_days=4` and the Ensemble API request SHALL use `forecast_days=15`

#### Scenario: Weather setting no longer exists
- **WHEN** `Njord:Weather:ForecastDays` is configured
- **THEN** it SHALL have no effect (the section is removed)

#### Scenario: Options placement
- **WHEN** the solution is inspected
- **THEN** ForecastOptions, EnsembleOptions, and FloodOptions (plus their validators) SHALL be in `Njord.OpenMeteo/Configuration/`, alongside AirQualityOptions, MarineOptions, SeasonalOptions, and SatelliteOptions
