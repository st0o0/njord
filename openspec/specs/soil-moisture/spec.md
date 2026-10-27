# soil-moisture Specification

## Purpose

SoilMoisture condition reporting topsoil moisture status (dry/optimal/wet) for gardening and irrigation guidance.

## Requirements

### Requirement: SoilMoisture reports topsoil moisture status
The system SHALL provide a `SoilMoisture` condition in `Lifestyle` that reports topsoil moisture as a sensor with dry/optimal/wet status and the raw volumetric value.

#### Scenario: Dry soil
- **WHEN** `soil_moisture_0_to_1cm` < 0.15 m3/m3
- **THEN** the sensor SHALL report status "dry"

#### Scenario: Optimal soil
- **WHEN** `soil_moisture_0_to_1cm` is between 0.15 and 0.35 m3/m3
- **THEN** the sensor SHALL report status "optimal"

#### Scenario: Wet soil
- **WHEN** `soil_moisture_0_to_1cm` > 0.35 m3/m3
- **THEN** the sensor SHALL report status "wet"

### Requirement: SoilMoisture config
`SoilMoistureConfig` SHALL have `Enabled` (default false), `DryThreshold` (default 0.15), and `WetThreshold` (default 0.35).

#### Scenario: Custom thresholds
- **WHEN** the operator sets `DryThreshold=0.10`
- **THEN** dry status SHALL trigger below 0.10 m3/m3
