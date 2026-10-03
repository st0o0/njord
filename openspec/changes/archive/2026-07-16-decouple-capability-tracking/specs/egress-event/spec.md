## MODIFIED Requirements

### Requirement: EgressEvent is a protocol-neutral discriminated union

The system SHALL define `EgressEvent` as an abstract record in `Njord.Egress`
with the following sealed variants:

- `PerModelUpdate(string Location, WeatherModel Model, ModelForecast Forecast)` — carries the typed domain forecast, not serialized JSON.
- `EnrichmentUpdate(string Location, string TypeName, object Result)` —
  replaces the 7 type-specific enrichment records (`ConsensusUpdate`,
  `AlertUpdate`, `DerivedUpdate`, `TrendUpdate`, `IndexUpdate`,
  `EnergyUpdate`, `HistoryUpdate`).
- `CapabilityLearned(string Location, WeatherModel Model, IReadOnlySet<ParameterDef> SupportedParameters, IReadOnlyList<int> ApplicableHorizons, IReadOnlyList<int> ApplicableDayOffsets)` — carries the full capability state for a (location, model) pair, emitted when the tracked parameter set changes.

The `MqttEgressActor` SHALL dispatch `EnrichmentUpdate` events by looking up
the `IEnrichmentFeature` whose `TypeName` matches `EnrichmentUpdate.TypeName`
and calling `feature.ToStateMessages(result, baseTopic)`.

#### Scenario: EgressEvent carries domain data only
- **WHEN** an `EgressEvent` variant is constructed
- **THEN** it SHALL contain only domain types — no references to `Njord.Mqtt`

#### Scenario: EnrichmentUpdate replaces type-specific records
- **WHEN** the enrichment actor produces a consensus result for location
  "lucerne"
- **THEN** it SHALL emit `EgressEvent.EnrichmentUpdate("lucerne", "consensus",
  result)` — not `EgressEvent.ConsensusUpdate`

#### Scenario: MqttEgressActor dispatches via feature registry
- **WHEN** `MqttEgressActor` receives an `EnrichmentUpdate` with
  `TypeName = "alerts"`
- **THEN** it SHALL find the `IEnrichmentFeature` with `TypeName == "alerts"`
  and call `feature.ToStateMessages(result, baseTopic)` to produce MQTT
  messages

#### Scenario: PerModelUpdate carries typed ModelForecast
- **WHEN** `ModelStateActor` produces a per-model update
- **THEN** it SHALL emit `EgressEvent.PerModelUpdate` with the `ModelForecast` domain object, not serialized horizon payloads

#### Scenario: CapabilityLearned carries full state for a model
- **WHEN** `ModelStateActor` detects a new or expanded parameter set for (lucerne, icon_d2)
- **THEN** it SHALL emit `EgressEvent.CapabilityLearned` with the complete supported parameters, applicable horizons, and applicable day-offsets

### Requirement: ModelStateActor produces PerModelUpdate events

The `ModelStateActor` SHALL reside in `Njord.Egress`, subscribe to the Pipeline BroadcastHub via `ISourceRef<FetchOutcome>`, transform `FetchOutcome.Success` into `EgressEvent.PerModelUpdate`, and send them into the EgressActor's MergeHub via `ISinkRef<EgressEvent>`. It SHALL NOT reference any types from `Njord.Mqtt`. After each successful fetch, it SHALL track which parameters the model delivered with non-null values and emit an `EgressEvent.CapabilityLearned` into the same egress sink when the tracked set changes.

#### Scenario: Successful fetch produces PerModelUpdate
- **WHEN** `ModelStateActor` receives a `FetchOutcome.Success` from the pipeline
- **THEN** it SHALL emit an `EgressEvent.PerModelUpdate` with the location, model, and `ModelForecast` domain object

#### Scenario: Fetch failures are dropped
- **WHEN** `ModelStateActor` receives a `FetchOutcome.Failure`
- **THEN** it SHALL NOT emit any `EgressEvent`

#### Scenario: First successful fetch emits CapabilityLearned
- **WHEN** `ModelStateActor` processes the first `FetchOutcome.Success` for a (location, model) pair
- **THEN** it SHALL emit an `EgressEvent.CapabilityLearned` into the egress sink with the observed supported parameters and applicable horizons

#### Scenario: ModelStateActor has no Njord.Mqtt dependency
- **WHEN** `ModelStateActor` is compiled
- **THEN** it SHALL NOT have a `using Njord.Mqtt` directive or resolve any actor from the `Njord.Mqtt` namespace
