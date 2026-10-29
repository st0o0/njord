## MODIFIED Requirements

### Requirement: Independent forecast days per domain
AirQuality, Marine, Flood, Forecast, and Ensemble SHALL each have their own configuration section with a forecast-days setting. No domain's forecast days SHALL affect another domain.

#### Scenario: Defaults
- **WHEN** no value is configured
- **THEN** Forecast SHALL use 4, Ensemble 4, AirQuality 5, Marine 8, and Flood 92 forecast days

#### Scenario: Forecast and Ensemble independent
- **WHEN** Forecast ForecastDays is 4 and Ensemble ForecastDays is 15
- **THEN** the Forecast API request SHALL use `forecast_days=4` and the Ensemble API request SHALL use `forecast_days=15`

#### Scenario: Weather setting no longer exists
- **WHEN** `Njord:Weather:ForecastDays` is configured
- **THEN** it SHALL have no effect (the section is removed)

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
