## ADDED Requirements

### Requirement: OceanCurrents reports current conditions
The system SHALL provide an `OceanCurrents` condition in `Marine` that reports ocean current velocity and direction as a sensor.

#### Scenario: Current data available
- **WHEN** the Marine API returns `ocean_current_velocity` and `ocean_current_direction`
- **THEN** the sensor SHALL report velocity in km/h and direction in degrees

#### Scenario: Strong current warning
- **WHEN** `ocean_current_velocity` exceeds a configurable threshold (default 3.0 km/h)
- **THEN** the sensor SHALL indicate a strong current condition

#### Scenario: Marine data unavailable
- **WHEN** the Marine API returns no data (inland location)
- **THEN** the feature SHALL produce no result

### Requirement: OceanCurrents config
`OceanCurrentsConfig` SHALL have `Enabled` (default false), `StrongCurrentThresholdKmh` (default 3.0).

#### Scenario: Custom threshold
- **WHEN** the operator sets `StrongCurrentThresholdKmh=5`
- **THEN** strong current indication SHALL trigger above 5 km/h
