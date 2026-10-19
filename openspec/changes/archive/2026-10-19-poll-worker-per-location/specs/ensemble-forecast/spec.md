## ADDED Requirements

### Requirement: Ensemble Mean endpoint returns mean and standard deviation
The OpenMeteoClient SHALL support the Ensemble Mean API endpoint (/v1/ensemble), returning pre-computed mean and standard deviation for all requested parameters.

#### Scenario: Ensemble response parsed
- **WHEN** the Ensemble Mean API returns hourly data with mean and std dev columns
- **THEN** the response SHALL be parsed into a DomainForecast<WeatherPoint> with EnsembleStdDev populated
