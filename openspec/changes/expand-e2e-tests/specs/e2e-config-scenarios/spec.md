## ADDED Requirements

### Requirement: Setup recipes for deterministic test scenarios

The E2E test plan SHALL define named setup recipes that configure the stack in specific states for targeted testing.

#### Scenario: Default recipe (baseline)
- **WHEN** the "default" recipe is used
- **THEN** the stack runs with 1 location (lucerne), 2 models (icon_d2, ecmwf_ifs025), all 6 enrichments enabled, default horizons (+3/+6/+12/+24/+48/+72h)

#### Scenario: Single-model recipe
- **WHEN** the "single-model" recipe is applied via `AdminService/SetLocations` or Docker environment override
- **THEN** only 1 model is active, and the entity count drops accordingly (no consensus entity, fewer target sensors)

#### Scenario: Disabled-enrichments recipe
- **WHEN** the "no-enrichments" recipe is applied via `AdminService/SetEnrichment` disabling all enrichment features
- **THEN** alert, index, trend, derived, and history sensors are absent or unavailable; weather entities remain functional

### Requirement: Hot-reload via AdminService

The E2E test plan SHALL verify that runtime config changes via AdminService take effect without container restart.

#### Scenario: Disable enrichment feature at runtime
- **WHEN** `AdminService/SetEnrichment` disables the "trends" feature
- **THEN** `sensor.lucerne_weather_trend` becomes unavailable or is removed from HA within 60 seconds, without restarting njord

#### Scenario: Re-enable enrichment feature at runtime
- **WHEN** `AdminService/SetEnrichment` re-enables the "trends" feature after disabling it
- **THEN** `sensor.lucerne_weather_trend` reappears in HA within 120 seconds (after the next poll cycle)

#### Scenario: Change poll settings at runtime
- **WHEN** `AdminService/SetSettings` modifies a setting
- **THEN** `AdminService/GetConfig` reflects the updated setting and `StreamConfig` delivers the change event

### Requirement: Config-driven entity set verification

The E2E test plan SHALL verify that the entity set adjusts to configuration changes.

#### Scenario: Adding a location creates new entities
- **WHEN** `AdminService/SetLocations` adds a second location (e.g., "zurich")
- **THEN** new weather and enrichment entities for that location appear in HA after the next poll cycle

#### Scenario: Entity count matches config
- **WHEN** the configuration is changed (models added/removed, enrichments toggled)
- **THEN** the total entity count in HA adjusts to match the new config within 2 poll cycles
