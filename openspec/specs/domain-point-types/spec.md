# domain-point-types Specification

## Purpose

Domain-specific forecast point types for each Open-Meteo API endpoint: WeatherPoint, EnsemblePoint, AirQualityPoint, FloodPoint, SolarPoint, and MarinePoint. Each implements IHasValidTime for temporal correlation across domains.

## Requirements

### Requirement: Domain point types implement IHasValidTime
Each domain point type (WeatherPoint, EnsemblePoint, AirQualityPoint, FloodPoint, SolarPoint, MarinePoint) SHALL implement IHasValidTime with a ValidFrom property.

#### Scenario: WeatherPoint has valid time
- **WHEN** a WeatherPoint is created from a Forecast API hourly data point
- **THEN** its ValidFrom SHALL be the UTC timestamp of that hourly slot

### Requirement: WeatherPoint carries ensemble statistics
WeatherPoint SHALL include mean values for all weather parameters. WeatherPoint SHALL NOT contain `EnsembleStdDev`, `PrecipitationSpread`, or `WindSpeedSpread` -- these fields move to `EnsemblePoint`.

#### Scenario: Ensemble mean and std dev
- **WHEN** a WeatherPoint is created from the Forecast API
- **THEN** it SHALL carry the deterministic forecast values and SHALL NOT have spread fields

### Requirement: EnsemblePoint carries spread fields
The system SHALL provide an `EnsemblePoint` record implementing `IHasValidTime` with `ValidFrom`, `TemperatureSpread`, `PrecipitationSpread`, and `WindSpeedSpread` -- all nullable doubles. A lookup method SHALL return the spread at a given timestamp for use by alert rules.

#### Scenario: EnsemblePoint created from Ensemble Mean API
- **WHEN** the Ensemble Mean API returns `temperature_2m_spread` for an hourly slot
- **THEN** `EnsemblePoint.TemperatureSpread` SHALL carry that value at the matching `ValidFrom`

#### Scenario: Spread lookup by timestamp
- **WHEN** an alert rule looks up spread at a WeatherPoint's ValidFrom
- **THEN** the lookup SHALL return the EnsemblePoint with the closest matching timestamp, or null if no ensemble data exists

### Requirement: EnsembleParams flags enum
The system SHALL provide an `EnsembleParams` flags enum with members for each spread variable (`TemperatureSpread`, `PrecipitationSpread`, `WindSpeedSpread`) and a `Models` member for comparison model data.

#### Scenario: Feature requires only temperature spread
- **WHEN** a feature declares `Ensemble: EnsembleParams.TemperatureSpread`
- **THEN** the ensemble request SHALL include `temperature_2m_spread`

#### Scenario: ModelComparison requires Models
- **WHEN** the ModelComparison feature is enabled
- **THEN** it SHALL declare `Ensemble: EnsembleParams.Models`
