# enrichment-actor Delta Specification

## MODIFIED Requirements

### Requirement: EnrichmentActor fans out enrichment results to EgressActor
The `EnrichmentActor` SHALL send computed enrichment results (ConsensusResult, AlertResult, DerivedResult, TrendResult, IndexResult, EnergyResult, HistoryResult) to the `EgressActor` as `PublishStateResult` messages. It SHALL NOT produce `MqttMessage` instances directly. It SHALL NOT reference `MqttMessage`, `TopicScheme`, or any type from `Njord.Mqtt`.

#### Scenario: Consensus result routed through EgressActor
- **WHEN** the consensus stream computes a `ConsensusResult`
- **THEN** `EnrichmentActor` sends `PublishStateResult(location, result)` to `EgressActor`

#### Scenario: No direct MqttMessage production
- **WHEN** any enrichment stream produces a result
- **THEN** the result is a domain record from `Njord.Domain.Analysis`, not an `MqttMessage`

### Requirement: Enrichment streams sink to EgressActor instead of MergeHub
Each enrichment consumer stream (consensus, alerts, derived, trends, indices, energy, history) SHALL end with a `Tell` to `EgressActor` carrying the domain result, instead of pushing `MqttMessage` instances into a MergeHub `SinkRef`. The `EnrichmentActor` SHALL resolve `EgressActor` via `Context.GetActor<EgressActor>()`.

#### Scenario: EnrichmentActor resolves EgressActor
- **WHEN** `EnrichmentActor` starts
- **THEN** it resolves `EgressActor` via `Context.GetActor<EgressActor>()`

#### Scenario: Stream consumer sends domain result to EgressActor
- **WHEN** the alert stream computes an `AlertResult`
- **THEN** `EnrichmentActor` tells `EgressActor` with `PublishStateResult(location, alertResult)`
