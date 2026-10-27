# coastal-flood Specification

## Purpose

CoastalFlood alert evaluating storm surge risk from the combination of sea level deviation, wave height, and onshore wind across Marine and Weather domains.

## Requirements

### Requirement: CoastalFlood alert combines marine and weather conditions for storm surge risk
The system SHALL provide a `CoastalFlood` alert in the Marine category that evaluates storm surge risk from sea level deviation, wave height, and onshore wind. The alert SHALL fire when sea_level_height_msl exceeds a configurable deviation threshold and wave conditions reinforce the risk.

#### Scenario: High sea level with large waves triggers alert
- **WHEN** sea_level_height_msl deviation exceeds 0.5m above normal and wave_height exceeds 2.0m and wind is onshore
- **THEN** the alert SHALL fire with severity proportional to the combined risk

#### Scenario: High sea level alone produces lower severity
- **WHEN** sea_level_height_msl deviation exceeds 0.5m but waves are small and wind is offshore
- **THEN** the alert SHALL fire with Yellow severity (sea level alone is a risk factor)

#### Scenario: Normal sea level does not trigger
- **WHEN** sea_level_height_msl deviation is below threshold regardless of wave conditions
- **THEN** the alert SHALL NOT fire

#### Scenario: Cross-domain input
- **WHEN** the CoastalFlood feature is enabled
- **THEN** it SHALL require both Marine (sea_level_height_msl, wave_height, wave_direction) and Weather (wind_speed, wind_direction) domains

#### Scenario: Ensemble provides confidence
- **WHEN** the CoastalFlood alert fires and ensemble spread data is available
- **THEN** confidence SHALL be derived from the ensemble temperature spread

#### Scenario: Inland location
- **WHEN** the Marine domain returns no data for an inland location
- **THEN** the CoastalFlood alert SHALL not fire (Project returns null)

### Requirement: CoastalFlood config
The `CoastalFloodConfig` SHALL support configurable sea level deviation threshold (default 0.3m), wave height threshold (default 1.5m), and wind speed threshold (default 15 km/h). Section `Njord:Features:Marine:CoastalFlood`. Default disabled.

#### Scenario: Config section
- **WHEN** the operator enables CoastalFlood
- **THEN** the config SHALL be read from `Njord:Features:Marine:CoastalFlood`
