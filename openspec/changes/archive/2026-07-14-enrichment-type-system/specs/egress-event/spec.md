## MODIFIED Requirements

### Requirement: EgressEvent is a protocol-neutral discriminated union

The system SHALL define `EgressEvent` as an abstract record in `Njord.Egress`
with the following sealed variants:

- `PerModelUpdate(string Location, WeatherModel Model,
  IReadOnlyDictionary<string, string> HorizonPayloads)` — unchanged.
- `EnrichmentUpdate(string Location, string TypeName, object Result)` —
  replaces the 7 type-specific enrichment records (`ConsensusUpdate`,
  `AlertUpdate`, `DerivedUpdate`, `TrendUpdate`, `IndexUpdate`,
  `EnergyUpdate`, `HistoryUpdate`).

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

#### Scenario: PerModelUpdate is unchanged
- **WHEN** `ModelStateActor` produces a per-model update
- **THEN** it SHALL still emit `EgressEvent.PerModelUpdate` with the same
  structure as before
