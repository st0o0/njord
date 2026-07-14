# enrichment-actor Specification

## Purpose

The EnrichmentActor consumes the pipeline's BroadcastHub via SourceRef, maintains a running ModelSnapshot via Scan, fans out to consumer streams through a second BroadcastHub, and routes computed domain results to the EgressActor as `EgressEvent` variants via StreamRef. Consumer streams are materialized only when enabled in configuration.

## Requirements

### Requirement: The EnrichmentActor requests a SourceRef from the PipelineActor
The `EnrichmentActor` SHALL send a `RequestPipelineSource` message to the `PipelineActor` on startup. Upon receiving a `PipelineSourceResponse`, it SHALL transition from `WaitingForSourceRef` to its operational state. Messages received before the SourceRef arrives SHALL be stashed.

#### Scenario: SourceRef received transitions to operational
- **WHEN** the EnrichmentActor starts and receives a `PipelineSourceResponse`
- **THEN** it transitions to its operational state and unstashes pending messages

#### Scenario: Messages are stashed before SourceRef
- **WHEN** the EnrichmentActor receives messages before the `PipelineSourceResponse`
- **THEN** the messages are stashed and replayed after the transition

#### Scenario: PipelineActor restart triggers re-request
- **WHEN** the EnrichmentActor receives a `Terminated` message for the PipelineActor
- **THEN** it sends a new `RequestPipelineSource` to the restarted PipelineActor

### Requirement: The EnrichmentActor maintains a ModelSnapshot via Scan
The `EnrichmentActor` SHALL materialize a consumer on the pipeline's `SourceRef<FetchOutcome>` using a `Scan` operator to accumulate a `ModelSnapshot`. Each `FetchOutcome.Success` SHALL update the snapshot with the contained `ModelForecast`. `FetchOutcome.Failure` elements SHALL not modify the snapshot. Only snapshots where `HasChanged` is `true` SHALL propagate downstream.

#### Scenario: Success updates the snapshot
- **WHEN** a `FetchOutcome.Success` for (lucerne, icon_d2) arrives
- **THEN** the snapshot is updated with that forecast and emitted downstream

#### Scenario: Failure does not change the snapshot
- **WHEN** a `FetchOutcome.Failure` arrives
- **THEN** the snapshot remains unchanged and no element is emitted

#### Scenario: Unchanged data is filtered
- **WHEN** a `FetchOutcome.Success` arrives with data identical to what is already in the snapshot
- **THEN** `HasChanged` is `false` and the snapshot is not emitted to consumers

### Requirement: The EnrichmentActor fans out via a second BroadcastHub
The `EnrichmentActor` SHALL materialize a `BroadcastHub<ModelSnapshot>` from the Scan output. Consumer streams SHALL each independently subscribe to this BroadcastHub. Each consumer SHALL receive every changed snapshot.

#### Scenario: Two consumers receive the same snapshot
- **WHEN** a changed `ModelSnapshot` enters the BroadcastHub and two consumers are subscribed
- **THEN** both consumers receive the snapshot independently

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

### Requirement: Consumer streams are materialized only when enabled
The `EnrichmentActor` SHALL read `EnrichmentOptions` from configuration. For each consumer type (e.g. `Consensus`), if `Enabled` is `false`, the consumer stream SHALL NOT be materialized — no BroadcastHub subscriber is created. If `Enabled` is `true`, the consumer stream SHALL be materialized and connected to the BroadcastHub and EgressActor SinkRef.

#### Scenario: Disabled consumer is not materialized
- **WHEN** `EnrichmentOptions.Consensus.Enabled` is `false`
- **THEN** no consensus consumer stream is materialized; the BroadcastHub has no subscriber for consensus

#### Scenario: Enabled consumer is materialized
- **WHEN** `EnrichmentOptions.Consensus.Enabled` is `true`
- **THEN** the consensus consumer stream is materialized and connected to the BroadcastHub

### Requirement: Enrichment streams sink to EgressActor instead of MergeHub

Each enrichment consumer stream SHALL use `RunWith(egressSinkRef.Sink, mat)` to deliver `EgressEvent` instances to the EgressActor's MergeHub. The stream graphs SHALL NOT maintain their own dedup dictionaries — deduplication is the responsibility of the downstream protocol-specific consumers.

#### Scenario: Consumer graph terminates at EgressActor sink
- **WHEN** an enrichment consumer sub-graph is materialized
- **THEN** its terminal sink SHALL be the `ISinkRef<EgressEvent>` obtained from the EgressActor

#### Scenario: No per-consumer dedup in enrichment
- **WHEN** an enrichment sub-graph produces an `EgressEvent` with the same payload as a previous emission
- **THEN** the enrichment sub-graph SHALL still emit it — dedup is downstream

### Requirement: Stream supervision resumes on consumer errors
Each consumer stream SHALL use a supervision strategy that resumes on exceptions. A failure in one consumer's computation SHALL NOT terminate other consumer streams or the snapshot BroadcastHub.

#### Scenario: Consumer exception does not kill the pipeline
- **WHEN** the consensus consumer throws during one computation
- **THEN** the stream resumes and processes the next snapshot; other consumers are unaffected

### Requirement: The EnrichmentActor materializes an alert consumer stream when enabled
The `EnrichmentActor` SHALL materialize an alert consumer stream when `EnrichmentOptions.Alerts.Enabled` is `true`. The stream SHALL subscribe to the `BroadcastHub<ModelSnapshot>`, evaluate all alert types via `AlertEvaluator`, wrap results in the corresponding `EgressEvent` variant, and sink into the EgressActor's SinkRef. If `Alerts.Enabled` is `false`, no alert consumer stream SHALL be materialized.

#### Scenario: Alert consumer alongside consensus
- **WHEN** both `Consensus.Enabled` and `Alerts.Enabled` are `true`
- **THEN** two consumer streams subscribe to the BroadcastHub independently

#### Scenario: Alert consumer only
- **WHEN** `Consensus.Enabled` is `false` and `Alerts.Enabled` is `true`
- **THEN** only the alert consumer stream subscribes to the BroadcastHub

#### Scenario: Alert consumer disabled
- **WHEN** `Alerts.Enabled` is `false`
- **THEN** no alert consumer stream is materialized

### Requirement: The EnrichmentActor materializes a derived consumer stream when enabled
The `EnrichmentActor` SHALL materialize a derived consumer stream when `EnrichmentOptions.Derived.Enabled` is `true`. The stream SHALL subscribe to the `BroadcastHub<ModelSnapshot>`, compute all derived values via `DerivedResult.Compute`, wrap results in the corresponding `EgressEvent` variant, and sink into the EgressActor's SinkRef. If `Derived.Enabled` is `false`, no derived consumer stream SHALL be materialized.

#### Scenario: Derived consumer alongside consensus and alerts
- **WHEN** `Consensus.Enabled`, `Alerts.Enabled`, and `Derived.Enabled` are all `true`
- **THEN** three consumer streams subscribe to the BroadcastHub independently

#### Scenario: Derived consumer only
- **WHEN** `Consensus.Enabled` and `Alerts.Enabled` are `false` and `Derived.Enabled` is `true`
- **THEN** only the derived consumer stream subscribes to the BroadcastHub

#### Scenario: Derived consumer disabled
- **WHEN** `Derived.Enabled` is `false`
- **THEN** no derived consumer stream is materialized

### Requirement: The EnrichmentActor materializes a trend consumer stream when enabled
The `EnrichmentActor` SHALL materialize a trend consumer stream when `EnrichmentOptions.Trends.Enabled` is `true`. The stream SHALL subscribe to the `BroadcastHub<ModelSnapshot>`, use a `Scan` operator to carry a `(ModelSnapshot? Previous, ModelSnapshot Current)` pair, compute trends via `TrendResult.Compute` when a previous snapshot exists, wrap results in the corresponding `EgressEvent` variant, and sink into the EgressActor's SinkRef. If `Trends.Enabled` is `false`, no trend consumer stream SHALL be materialized. The first snapshot after materialization SHALL produce no trend output (no previous to compare against).

#### Scenario: Trend consumer with scan pairing
- **WHEN** `Trends.Enabled` is `true` and two consecutive snapshots arrive
- **THEN** the trend consumer computes trends comparing the second snapshot to the first

#### Scenario: First snapshot produces no output
- **WHEN** `Trends.Enabled` is `true` and the first snapshot arrives
- **THEN** no trend messages are emitted (no previous snapshot for comparison)

#### Scenario: Trend consumer disabled
- **WHEN** `Trends.Enabled` is `false`
- **THEN** no trend consumer stream is materialized

### Requirement: The EnrichmentActor materializes an index consumer stream when enabled
The `EnrichmentActor` SHALL materialize an index consumer stream when `EnrichmentOptions.Indices.Enabled` is `true`. The stream SHALL subscribe to the `BroadcastHub<ModelSnapshot>`, compute all indices via `IndexResult.Compute`, wrap results in the corresponding `EgressEvent` variant, and sink into the EgressActor's SinkRef. If `Indices.Enabled` is `false`, no index consumer stream SHALL be materialized.

#### Scenario: Index consumer enabled
- **WHEN** `Indices.Enabled` is `true`
- **THEN** the index consumer stream subscribes to the BroadcastHub

#### Scenario: Index consumer disabled
- **WHEN** `Indices.Enabled` is `false`
- **THEN** no index consumer stream is materialized

### Requirement: The EnrichmentActor materializes an energy consumer stream when enabled
The `EnrichmentActor` SHALL materialize an energy consumer stream when `EnrichmentOptions.Energy.Enabled` is `true`. The stream SHALL subscribe to the `BroadcastHub<ModelSnapshot>`, compute all energy values via `EnergyResult.Compute`, wrap results in the corresponding `EgressEvent` variant, and sink into the EgressActor's SinkRef. If `Energy.Enabled` is `false`, no energy consumer stream SHALL be materialized.

#### Scenario: Energy consumer enabled
- **WHEN** `Energy.Enabled` is `true`
- **THEN** the energy consumer stream subscribes to the BroadcastHub

#### Scenario: Energy consumer disabled
- **WHEN** `Energy.Enabled` is `false`
- **THEN** no energy consumer stream is materialized

### Requirement: The EnrichmentActor materializes a history consumer stream when enabled
The `EnrichmentActor` SHALL materialize a history consumer stream when `EnrichmentOptions.History.Enabled` is `true`. The stream SHALL subscribe to the `BroadcastHub<ModelSnapshot>`, forward each snapshot to the `ForecastHistoryActor` for persistence, query the actor for history state, compute analysis via `HistoryAnalyzer`, wrap results in the corresponding `EgressEvent` variant, and sink into the EgressActor's SinkRef. The `ForecastHistoryActor` SHALL be created per location as a child of the `EnrichmentActor`. If `History.Enabled` is `false`, no history consumer stream or history actors SHALL be materialized.

#### Scenario: History consumer enabled
- **WHEN** `History.Enabled` is `true`
- **THEN** the history consumer stream and per-location ForecastHistoryActors are materialized

#### Scenario: History consumer disabled
- **WHEN** `History.Enabled` is `false`
- **THEN** no history consumer stream or history actors are materialized

#### Scenario: History actors are children of EnrichmentActor
- **WHEN** the history consumer is enabled with 2 locations
- **THEN** 2 ForecastHistoryActor children exist, one per location
