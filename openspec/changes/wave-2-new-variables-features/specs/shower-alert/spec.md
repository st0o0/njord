## ADDED Requirements

### Requirement: ShowerAlert triggers on convective precipitation
The system SHALL provide a `ShowerAlert` feature rule in `Alerts` with category `Weather` that fires when convective showers exceed a configurable threshold (default 5 mm/h) within the forecast window.

#### Scenario: Showers above threshold
- **WHEN** the forecast contains an hour with `showers` >= 5 mm/h
- **THEN** the alert SHALL fire with severity based on the peak shower rate

#### Scenario: Showers below threshold
- **WHEN** no forecast hour has `showers` >= 5 mm/h
- **THEN** the alert SHALL not fire

### Requirement: ShowerAlert requires Ensemble for confidence
ShowerAlert SHALL declare `Ensemble: EnsembleParams.TemperatureSpread` in its requirements and derive confidence from ensemble spread.

#### Scenario: Confidence from ensemble
- **WHEN** the alert evaluates with ensemble data
- **THEN** confidence SHALL be derived from the precipitation spread at the peak hour

### Requirement: ShowerAlert config
`ShowerConfig` SHALL have `Enabled` (default false), `ThresholdMmPerHour` (default 5.0), and severity step thresholds.

#### Scenario: Custom threshold
- **WHEN** the operator configures `ThresholdMmPerHour=10`
- **THEN** the alert SHALL only fire when showers exceed 10 mm/h
