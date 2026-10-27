# pollen-blast Specification

## Purpose

PollenBlast alert detecting pollen explosion conditions after a rain-to-dry weather transition, using trend analysis across recent poll cycles.

## Requirements

### Requirement: PollenBlast alert detects pollen explosion after rain-to-dry transition
The system SHALL provide a `PollenBlast` alert in the AirQuality category that detects conditions favoring a pollen release event: warm temperatures (>15C), dry conditions, and moderate wind following a period of rain. The alert uses PollDepth=8 to analyze the rain-to-dry transition across recent poll cycles.

#### Scenario: Warm dry windy after rain triggers alert
- **WHEN** the previous 2+ polls showed precipitation and the current poll shows no precipitation with temperature above 15C and wind speed above 5 km/h, and at least one pollen type shows elevated concentration
- **THEN** the alert SHALL fire with severity proportional to the pollen concentration increase

#### Scenario: Continuous dry does not trigger
- **WHEN** the weather has been dry for all recent polls (no rain-to-dry transition)
- **THEN** the alert SHALL NOT fire even if pollen concentrations are high

#### Scenario: No pollen data available
- **WHEN** weather conditions match but no pollen data is available (outside pollen season or non-European location)
- **THEN** the alert SHALL NOT fire

#### Scenario: Cross-domain input
- **WHEN** the PollenBlast feature is enabled
- **THEN** it SHALL require both Weather (temperature, precipitation, wind speed) and AirQuality (pollen parameters) domains

### Requirement: PollenBlast config
The `PollenBlastConfig` SHALL support configurable temperature threshold (default 15C), minimum wind speed (default 5 km/h), and minimum pollen concentration delta (default 10 grains/m3). Section `Njord:Features:AirQuality:PollenBlast`. Default disabled.

#### Scenario: Config section
- **WHEN** the operator enables PollenBlast
- **THEN** the config SHALL be read from `Njord:Features:AirQuality:PollenBlast`
