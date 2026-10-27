## ADDED Requirements

### Requirement: WetBulbDanger alert fires on dangerous wet-bulb temperature
The system SHALL provide a `WetBulbDanger` alert that evaluates `wet_bulb_temperature_2m` over the forecast horizon. The alert SHALL fire when the maximum wet-bulb temperature exceeds the configured threshold. Severity SHALL be derived from the peak value: yellow (>26 °C), orange (>28 °C), red (>32 °C). A wet-bulb temperature above 35 °C is lethal; 28 °C is dangerous for prolonged outdoor activity.

#### Scenario: Dangerous wet-bulb temperature
- **WHEN** `wet_bulb_temperature_2m` exceeds 28 °C in any forecast hour
- **THEN** the alert SHALL fire with severity orange

#### Scenario: Extreme wet-bulb temperature
- **WHEN** `wet_bulb_temperature_2m` exceeds 32 °C
- **THEN** the alert SHALL fire with severity red

#### Scenario: No danger
- **WHEN** all `wet_bulb_temperature_2m` values are below 26 °C
- **THEN** the alert SHALL not fire

### Requirement: WetBulbDanger requires Weather and Ensemble
The feature descriptor SHALL declare `Weather: WetBulbTemperature` and `Ensemble: TemperatureSpread` in its domain requirements.

#### Scenario: Ensemble unavailable
- **WHEN** ensemble data has not been polled
- **THEN** the alert SHALL not fire (required domain missing)

### Requirement: WetBulbDanger is disabled by default
The feature descriptor SHALL set `DefaultEnabled = false`. Wet-bulb temperatures above 26 °C are rare at most locations.

#### Scenario: Must be explicitly enabled
- **WHEN** no explicit configuration is provided
- **THEN** the alert SHALL not be active
