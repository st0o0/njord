## ADDED Requirements

### Requirement: RagweedPollen sensor
The system SHALL provide a `RagweedPollen` sensor in `AirQuality` that reports ragweed pollen concentration following the same pattern as existing pollen sensors (birch, grass, alder, mugwort, olive).

#### Scenario: Pollen value available
- **WHEN** the AQ API returns `ragweed_pollen` data for the current hour
- **THEN** the sensor SHALL report the numeric concentration in grains/m3

#### Scenario: Pollen value unavailable
- **WHEN** `ragweed_pollen` is null (outside pollen season or region)
- **THEN** the sensor SHALL report 0

### Requirement: RagweedPollen config
`RagweedPollenConfig` SHALL have `Enabled` (default false) following the existing pollen feature pattern.

#### Scenario: Default disabled
- **WHEN** no configuration is provided for RagweedPollen
- **THEN** the feature SHALL be disabled
