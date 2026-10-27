## ADDED Requirements

### Requirement: RainProbability alert fires on high precipitation probability
The system SHALL provide a `RainProbability` alert that evaluates `precipitation_probability` over a rolling window. The alert SHALL fire when the maximum probability in the next configurable hours (default 6) exceeds the configured threshold. Severity SHALL be derived from the peak probability: moderate (>50 %), high (>70 %), extreme (>90 %). The thresholds SHALL be configurable via `RainProbabilityConfig`.

#### Scenario: High rain probability triggers alert
- **WHEN** `precipitation_probability` exceeds 70 % in any hour within the next 6 hours
- **THEN** the alert SHALL fire with severity high

#### Scenario: Moderate probability
- **WHEN** the peak `precipitation_probability` is 55 % in the window
- **THEN** the alert SHALL fire with severity moderate

#### Scenario: No significant probability
- **WHEN** all `precipitation_probability` values in the window are below 50 %
- **THEN** the alert SHALL not fire

#### Scenario: Window is configurable
- **WHEN** `RainProbabilityConfig.WindowHours` is set to 3
- **THEN** only the next 3 hours of data SHALL be evaluated

### Requirement: RainProbability requires Weather and Ensemble
The feature descriptor SHALL declare `Weather: PrecipitationProbability` and `Ensemble: TemperatureSpread` in its domain requirements. The alert SHALL use ensemble spread for confidence calculation.

#### Scenario: Ensemble unavailable
- **WHEN** ensemble data has not been polled
- **THEN** the alert SHALL not fire (required domain missing)

### Requirement: RainProbability is enabled by default
The feature descriptor SHALL set `DefaultEnabled = true`.

#### Scenario: Default configuration
- **WHEN** no explicit configuration is provided for `Njord:Features:Weather:RainProbability`
- **THEN** the alert SHALL be enabled with default thresholds
