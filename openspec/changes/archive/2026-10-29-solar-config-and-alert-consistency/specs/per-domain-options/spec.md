## ADDED Requirements

### Requirement: SatelliteOptions owns panel orientation
`SatelliteOptions` (`Njord:Satellite`) SHALL contain `PastHours`, `Tilt` and `Azimuth`. Orientation is an API parameter and SHALL live with the domain options, not with a feature. There SHALL be no `SolarYieldOptions` class, no `SolarYieldOptionsValidator` and no `Njord:SolarYield` section; `PeakKw` and `SystemEfficiency` belong to `SolarYieldConfig` (`Njord:Features:SolarYield`).

#### Scenario: Satellite section contents
- **WHEN** `IOptions<SatelliteOptions>.Value` is read without configuration
- **THEN** `PastHours` SHALL be 6, `Tilt` 30 and `Azimuth` 180

#### Scenario: SolarYield section removed
- **WHEN** the service starts with `Njord:SolarYield:PeakKw` configured
- **THEN** the value SHALL have no effect and no `SolarYieldOptions` SHALL be registered

#### Scenario: One validator per options class
- **WHEN** `SatelliteOptions` is validated
- **THEN** `SatelliteOptionsValidator` SHALL cover `PastHours`, `Tilt` and `Azimuth`

### Requirement: ForecastOptions has no array defaults hook
`ForecastOptions` SHALL NOT implement `IHasArrayDefaults`. An empty `Models` array SHALL mean best_match.

#### Scenario: Empty models
- **WHEN** `Njord:Forecast:Models` is not configured
- **THEN** `Models` SHALL be empty and the Forecast request SHALL use best_match

#### Scenario: No interface
- **WHEN** `ForecastOptions` is inspected
- **THEN** it SHALL NOT implement `IHasArrayDefaults`
