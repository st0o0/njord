## MODIFIED Requirements

### Requirement: Setup recipes for deterministic test scenarios

The E2E test plan SHALL define named setup recipes that configure the stack in specific states for targeted testing.

#### Scenario: Default recipe (baseline)
- **WHEN** the "default" recipe is used
- **THEN** the stack runs with 1 location (lucerne), 2 models (icon_d2, ecmwf_ifs025), all 5 analysis features enabled, default horizons (+3/+6/+12/+24/+48/+72h)

#### Scenario: Single-model recipe
- **WHEN** the "single-model" recipe is applied via `AdminService/SetLocations` or Docker environment override
- **THEN** only 1 model is active, and the entity count drops accordingly (no consensus entity, fewer target sensors)

#### Scenario: Disabled-enrichments recipe
- **WHEN** the "no-enrichments" recipe is applied via `AdminService/SetEnrichment` disabling all enrichment features
- **THEN** alert, index, trend and derived sensors are absent or unavailable; weather entities remain functional
