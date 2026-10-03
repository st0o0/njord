## RENAMED Requirements

- FROM: `### Requirement: ModelStateActor emits ModelCapabilityLearned`
- TO: `### Requirement: ModelStateActor emits CapabilityLearned`
- FROM: `### Requirement: ModelCapabilityLearned is a full-state idempotent message`
- TO: `### Requirement: CapabilityLearned is a full-state idempotent message`

## MODIFIED Requirements

### Requirement: Horizon capping uses ModelCoverageRegistry
The applicable horizons in `EgressEvent.CapabilityLearned` SHALL be the intersection of the configured horizon list and the model's `MaxForecastHours` from `ModelCoverageRegistry`. Hourly horizons exceeding `MaxForecastHours` SHALL be excluded. Daily day-offsets exceeding `ceil(MaxForecastHours / 24) - 1` SHALL be excluded. For models not in the registry (unknown), all configured horizons SHALL be included.

#### Scenario: Short-range model excludes far horizons
- **WHEN** `icon_d2` has MaxForecastHours=48 and configured horizons are [3, 6, 12, 24, 48, 72]
- **THEN** applicable horizons SHALL be [3, 6, 12, 24, 48] and applicable day-offsets SHALL be [0, 1]

#### Scenario: Long-range model includes all horizons
- **WHEN** `ecmwf_ifs025` has MaxForecastHours=240 and configured horizons are [3, 6, 12, 24, 48, 72]
- **THEN** applicable horizons SHALL be [3, 6, 12, 24, 48, 72] and applicable day-offsets SHALL be [0, 1, 2, 3]

#### Scenario: Unknown model includes all horizons
- **WHEN** a model not in `ModelCoverageRegistry` is configured with horizons [3, 6, 12, 24, 48, 72]
- **THEN** applicable horizons SHALL include all configured horizons
