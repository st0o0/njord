## MODIFIED Requirements

### Requirement: EgressEvent is a protocol-neutral discriminated union

The system SHALL define `EgressEvent` as an abstract record in `Njord.Messages` (namespace `Njord.Messages.Egress`)
with the following sealed variants:

- `PerModelUpdate(string Location, WeatherModel Model, ModelForecast Forecast)` — carries the typed domain forecast, not serialized JSON.
- `EnrichmentUpdate(string Location, string TypeName, object Result, DateTimeOffset? UpdatedAt = null)` —
  replaces the 6 type-specific enrichment records (`ConsensusUpdate`,
  `AlertUpdate`, `DerivedUpdate`, `TrendUpdate`, `IndexUpdate`,
  `HistoryUpdate`). The optional `UpdatedAt` field carries
  the enrichment computation timestamp when available (e.g. from
  `ConsensusResult.ComputedAt`); when null, downstream consumers SHALL fall
  back to wall-clock time.
- `CapabilityLearned(string Location, WeatherModel Model, IReadOnlySet<ParameterDef> SupportedParameters, IReadOnlyList<int> ApplicableHorizons, IReadOnlyList<int> ApplicableDayOffsets)` — carries the full capability state for a (location, model) pair, emitted when the tracked parameter set changes.

The `MqttEgressActor` SHALL dispatch `EnrichmentUpdate` events by looking up
the `IEnrichmentPresenter` whose `TypeName` matches `EnrichmentUpdate.TypeName`
and calling `presenter.ToStateMessages(result, baseTopic, location)`.

#### Scenario: EgressEvent carries domain data only
- **WHEN** an `EgressEvent` variant is constructed
- **THEN** it SHALL contain only domain types — no references to `Njord.Mqtt`

#### Scenario: EnrichmentUpdate replaces type-specific records
- **WHEN** the enrichment actor produces a consensus result for location
  "lucerne"
- **THEN** it SHALL emit `EgressEvent.EnrichmentUpdate("lucerne", "consensus",
  result)` — not `EgressEvent.ConsensusUpdate`

#### Scenario: Consensus EnrichmentUpdate carries ComputedAt as UpdatedAt
- **WHEN** the enrichment actor produces a consensus result with `ComputedAt = 2026-07-31T06:00:00Z`
- **THEN** it SHALL emit `EgressEvent.EnrichmentUpdate` with `UpdatedAt = 2026-07-31T06:00:00Z`

#### Scenario: Non-consensus EnrichmentUpdate leaves UpdatedAt null
- **WHEN** the enrichment actor produces an alert result
- **THEN** it SHALL emit `EgressEvent.EnrichmentUpdate` with `UpdatedAt = null`

#### Scenario: MqttEgressActor dispatches via presenter registry
- **WHEN** `MqttEgressActor` receives an `EnrichmentUpdate` with
  `TypeName = "alerts"`
- **THEN** it SHALL find the `IEnrichmentPresenter` with `TypeName == "alerts"`
  and call `presenter.ToStateMessages(result, baseTopic, location)` to produce MQTT
  messages

#### Scenario: MqttEgressActor dispatches consensus via the same registry
- **WHEN** `MqttEgressActor` receives an `EnrichmentUpdate` with
  `TypeName = "consensus"` and a `ConsensusResult`
- **THEN** it SHALL find the `IEnrichmentPresenter` with `TypeName == "consensus"`
  and call `presenter.ToStateMessages(result, baseTopic, location)`, with no
  consensus-specific branch outside the registry

#### Scenario: PerModelUpdate carries typed ModelForecast
- **WHEN** `ModelStateActor` produces a per-model update
- **THEN** it SHALL emit `EgressEvent.PerModelUpdate` with the `ModelForecast` domain object, not serialized horizon payloads

#### Scenario: CapabilityLearned carries full state for a model
- **WHEN** `ModelStateActor` detects a new or expanded parameter set for (lucerne, icon_d2)
- **THEN** it SHALL emit `EgressEvent.CapabilityLearned` with the complete supported parameters, applicable horizons, and applicable day-offsets
