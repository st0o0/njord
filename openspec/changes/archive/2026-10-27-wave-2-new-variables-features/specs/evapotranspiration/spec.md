## ADDED Requirements

### Requirement: Evapotranspiration reports daily water loss
The system SHALL provide an `Evapotranspiration` condition in `Lifestyle` that reports cumulative ET0 over the current day as a sensor with irrigation guidance.

#### Scenario: Low evapotranspiration
- **WHEN** cumulative daily ET0 < 2 mm
- **THEN** the sensor SHALL report "low" water demand

#### Scenario: Moderate evapotranspiration
- **WHEN** cumulative daily ET0 is between 2 and 5 mm
- **THEN** the sensor SHALL report "moderate" water demand

#### Scenario: High evapotranspiration
- **WHEN** cumulative daily ET0 > 5 mm
- **THEN** the sensor SHALL report "high" water demand with irrigation recommendation

### Requirement: Evapotranspiration config
`EvapotranspirationConfig` SHALL have `Enabled` (default false) and thresholds `LowMm` (default 2.0), `HighMm` (default 5.0).

#### Scenario: Custom thresholds
- **WHEN** the operator sets `HighMm=4`
- **THEN** high demand SHALL trigger above 4 mm
