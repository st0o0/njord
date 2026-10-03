## MODIFIED Requirements

### Requirement: ModelStateActor emits ModelCapabilityLearned
After computing the parameter set from a successful fetch, the `ModelStateActor` SHALL emit an `EgressEvent.CapabilityLearned` into the EgressActor's MergeHub via the same `ISinkRef<EgressEvent>` used for `PerModelUpdate` whenever the tracked set changes (initial population or expansion). The event SHALL carry the full current state: location, model, the complete set of supported parameters, applicable hourly horizons (capped by `ModelCoverageRegistry.MaxForecastHours`), and applicable daily day-offsets (capped by `ceil(MaxForecastHours / 24)`). The standalone `ModelCapabilityLearned` record SHALL be removed.

#### Scenario: First fetch triggers capability event
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
