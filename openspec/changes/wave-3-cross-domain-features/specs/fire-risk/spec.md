## ADDED Requirements

### Requirement: FireRisk alert evaluates wildfire danger from multi-variable scoring
The system SHALL provide a `FireRisk` alert in the Lifestyle category that scores wildfire danger from five weather variables: temperature, humidity, wind speed, soil moisture, and recent precipitation. The alert SHALL fire when the combined score exceeds a configurable threshold.

#### Scenario: All conditions met produces Red severity
- **WHEN** temperature exceeds 35C, humidity is below 20%, wind speed exceeds 25 km/h, soil moisture is below 0.08 m3/m3, and no precipitation has occurred in the forecast window
- **THEN** the alert SHALL fire with Red severity

#### Scenario: Partial conditions produce Yellow severity
- **WHEN** temperature exceeds 30C and humidity is below 30% but wind is moderate and soil moisture is adequate
- **THEN** the alert SHALL fire with Yellow severity

#### Scenario: Adequate moisture prevents alert
- **WHEN** soil moisture exceeds 0.15 m3/m3 or recent precipitation exceeds 5mm
- **THEN** the alert SHALL NOT fire regardless of temperature and wind

#### Scenario: Ensemble provides confidence
- **WHEN** the FireRisk alert fires and ensemble spread data is available
- **THEN** confidence SHALL be derived from the ensemble temperature spread

### Requirement: FireRisk config
The `FireRiskConfig` SHALL support configurable thresholds for temperature (default 30C), humidity (default 30%), wind speed (default 20 km/h), soil moisture (default 0.10 m3/m3), and dry hours (default 48). Section `Njord:Features:Lifestyle:FireRisk`. Default disabled.

#### Scenario: Custom thresholds
- **WHEN** the operator sets temperature threshold to 35C
- **THEN** the alert SHALL use 35C instead of 30C for the temperature component
