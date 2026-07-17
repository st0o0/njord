# model-capability-tracking Specification

## Purpose

Runtime capability learning for weather models: ModelStateActor tracks which parameters each model actually delivers with non-null values, caps horizons by ModelCoverageRegistry, and emits ModelCapabilityLearned messages to drive capability-filtered discovery.

## Requirements

### Requirement: ModelStateActor learns parameter support from API responses
The `ModelStateActor` SHALL maintain a `HashSet<ParameterDef>` per (location, model) pair tracking which parameters the model has delivered with at least one non-null value. After each successful `FetchOutcome.Success`, the actor SHALL union the newly observed non-null parameters into the tracked set.

#### Scenario: First fetch establishes parameter baseline
- **WHEN** `ModelStateActor` processes the first `FetchOutcome.Success` for (lucerne, icon_d2) and the response contains non-null values for 25 of 30 requested parameters
- **THEN** the tracked set for (lucerne, icon_d2) SHALL contain exactly those 25 parameters

#### Scenario: Subsequent fetch with unchanged parameters
- **WHEN** `ModelStateActor` processes a second `FetchOutcome.Success` for (lucerne, icon_d2) with the same 25 non-null parameters
- **THEN** the tracked set SHALL remain unchanged (25 parameters)

#### Scenario: Late parameter appearance expands the set
- **WHEN** a parameter that was null on all prior fetches appears with a non-null value
- **THEN** the tracked set SHALL grow to include that parameter

### Requirement: ModelStateActor emits ModelCapabilityLearned
After computing the parameter set from a successful fetch, the `ModelStateActor` SHALL emit an `EgressEvent.CapabilityLearned` into the EgressActor's MergeHub via the same `ISinkRef<EgressEvent>` used for `PerModelUpdate` whenever the tracked set changes (initial population or expansion). The event SHALL carry the full current state: location, model, the complete set of supported parameters, applicable hourly horizons (capped by `ModelCoverageRegistry.MaxForecastHours`), and applicable daily day-offsets (capped by `ceil(MaxForecastHours / 24)`). The standalone `ModelCapabilityLearned` record SHALL be removed.

#### Scenario: First fetch triggers capability message
- **WHEN** `ModelStateActor` processes the first `FetchOutcome.Success` for (lucerne, icon_d2) with MaxForecastHours=48 and configured horizons [3, 6, 12, 24, 48, 72]
- **THEN** it SHALL emit `EgressEvent.CapabilityLearned` with supported parameters, applicable horizons [3, 6, 12, 24, 48], and applicable day-offsets [0, 1] into the egress sink

#### Scenario: Unchanged capability set does not re-emit
- **WHEN** `ModelStateActor` processes a `FetchOutcome.Success` whose non-null parameters are a subset of the already-tracked set
- **THEN** it SHALL NOT emit an `EgressEvent.CapabilityLearned`

#### Scenario: Expanded capability set triggers update
- **WHEN** a previously-null parameter appears with non-null values on a later fetch
- **THEN** `ModelStateActor` SHALL emit an updated `EgressEvent.CapabilityLearned` with the expanded parameter set

#### Scenario: Fetch failure does not affect tracked capabilities
- **WHEN** `ModelStateActor` receives a `FetchOutcome.Failure`
- **THEN** the tracked parameter set SHALL remain unchanged and no `EgressEvent.CapabilityLearned` SHALL be emitted

### Requirement: ModelCapabilityLearned is a full-state idempotent message
`EgressEvent.CapabilityLearned` SHALL be a sealed record carrying `Location` (string), `Model` (WeatherModel), `SupportedParameters` (IReadOnlySet<ParameterDef>), `ApplicableHorizons` (IReadOnlyList<int>), and `ApplicableDayOffsets` (IReadOnlyList<int>). It SHALL represent the complete known state for that (location, model) pair, not a delta.

#### Scenario: Message carries full state
- **WHEN** `EgressEvent.CapabilityLearned` is constructed for (lucerne, icon_d2) with 25 supported parameters and horizons [3, 6, 12, 24, 48]
- **THEN** the event SHALL contain all 25 parameters and all 5 horizons regardless of what was emitted in prior events

### Requirement: Horizon capping uses ModelCoverageRegistry
The applicable horizons in `ModelCapabilityLearned` SHALL be the intersection of the configured horizon list and the model's `MaxForecastHours` from `ModelCoverageRegistry`. Hourly horizons exceeding `MaxForecastHours` SHALL be excluded. Daily day-offsets exceeding `ceil(MaxForecastHours / 24) - 1` SHALL be excluded. For models not in the registry (unknown), all configured horizons SHALL be included.

#### Scenario: Short-range model excludes far horizons
- **WHEN** `icon_d2` has MaxForecastHours=48 and configured horizons are [3, 6, 12, 24, 48, 72]
- **THEN** applicable horizons SHALL be [3, 6, 12, 24, 48] and applicable day-offsets SHALL be [0, 1]

#### Scenario: Long-range model includes all horizons
- **WHEN** `ecmwf_ifs025` has MaxForecastHours=240 and configured horizons are [3, 6, 12, 24, 48, 72]
- **THEN** applicable horizons SHALL be [3, 6, 12, 24, 48, 72] and applicable day-offsets SHALL be [0, 1, 2, 3]

#### Scenario: Unknown model includes all horizons
- **WHEN** a model not in `ModelCoverageRegistry` is configured with horizons [3, 6, 12, 24, 48, 72]
- **THEN** applicable horizons SHALL include all configured horizons
