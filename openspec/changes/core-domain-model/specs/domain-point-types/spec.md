## ADDED Requirements

### Requirement: Domain point types implement IHasValidTime
Each domain point type (WeatherPoint, AirQualityPoint, FloodPoint, SolarPoint, MarinePoint) SHALL implement IHasValidTime with a ValidFrom property.

#### Scenario: WeatherPoint has valid time
- **WHEN** a WeatherPoint is created from an Ensemble API hourly data point
- **THEN** its ValidFrom SHALL be the UTC timestamp of that hourly slot

### Requirement: WeatherPoint carries ensemble statistics
WeatherPoint SHALL include mean values for all weather parameters plus optional EnsembleStdDev for uncertainty quantification.

#### Scenario: Ensemble mean and std dev
- **WHEN** a WeatherPoint is created from the Ensemble Mean API
- **THEN** it SHALL carry the mean temperature AND the standard deviation
