## MODIFIED Requirements

### Requirement: EnrichmentActor fans out enrichment results to EgressActor

Each enrichment consumer sub-graph SHALL wrap its computed domain result in the corresponding `EgressEvent` variant and send it to the EgressActor's MergeHub via `ISinkRef<EgressEvent>`. The `EnrichmentActor` SHALL request an `ISinkRef<EgressEvent>` from the `EgressActor` (via `RequestEgressSink`) instead of requesting an `ISinkRef<MqttMessage>` from `MqttConnectionActor`. The `EnrichmentActor` SHALL NOT reference any types from `Njord.Mqtt`.

#### Scenario: Consensus result produces ConsensusUpdate
- **WHEN** the consensus sub-graph computes a `ConsensusResult` for a location
- **THEN** it SHALL emit `EgressEvent.ConsensusUpdate(location, result)` into the EgressActor's MergeHub

#### Scenario: Alert result produces AlertUpdate
- **WHEN** the alert sub-graph evaluates alerts for a location
- **THEN** it SHALL emit `EgressEvent.AlertUpdate(location, result)` into the EgressActor's MergeHub

#### Scenario: Derived result produces DerivedUpdate
- **WHEN** the derived sub-graph computes derived values for a location
- **THEN** it SHALL emit `EgressEvent.DerivedUpdate(location, result)` into the EgressActor's MergeHub

#### Scenario: Trend result produces TrendUpdate
- **WHEN** the trend sub-graph computes trend analysis for a location
- **THEN** it SHALL emit `EgressEvent.TrendUpdate(location, result)` into the EgressActor's MergeHub

#### Scenario: Index result produces IndexUpdate
- **WHEN** the index sub-graph computes activity indices for a location
- **THEN** it SHALL emit `EgressEvent.IndexUpdate(location, result)` into the EgressActor's MergeHub

#### Scenario: Energy result produces EnergyUpdate
- **WHEN** the energy sub-graph computes energy management values for a location
- **THEN** it SHALL emit `EgressEvent.EnergyUpdate(location, result)` into the EgressActor's MergeHub

#### Scenario: History result produces HistoryUpdate
- **WHEN** the history sub-graph computes historical analysis for a location
- **THEN** it SHALL emit `EgressEvent.HistoryUpdate(location, result)` into the EgressActor's MergeHub

#### Scenario: No MQTT dependency
- **WHEN** the `EnrichmentActor` source file is compiled
- **THEN** it SHALL have no `using Njord.Mqtt` directive and no reference to `MqttMessage`, `MqttSinkResponse`, `RequestMqttSink`, or any other `Njord.Mqtt` type

### Requirement: Enrichment streams sink to EgressActor instead of MergeHub

Each enrichment consumer stream SHALL use `RunWith(egressSinkRef.Sink, mat)` to deliver `EgressEvent` instances to the EgressActor's MergeHub. The stream graphs SHALL NOT maintain their own dedup dictionaries — deduplication is the responsibility of the downstream protocol-specific consumers.

#### Scenario: Consumer graph terminates at EgressActor sink
- **WHEN** an enrichment consumer sub-graph is materialized
- **THEN** its terminal sink SHALL be the `ISinkRef<EgressEvent>` obtained from the EgressActor

#### Scenario: No per-consumer dedup in enrichment
- **WHEN** an enrichment sub-graph produces an `EgressEvent` with the same payload as a previous emission
- **THEN** the enrichment sub-graph SHALL still emit it — dedup is downstream
