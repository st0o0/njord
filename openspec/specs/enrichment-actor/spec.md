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

Each enrichment consumer sub-graph SHALL produce `EgressEvent.EnrichmentUpdate`
instances (carrying `Location`, `TypeName`, and `Result`) and send them to the
EgressActor's MergeHub via `ISinkRef<EgressEvent>`. The `EnrichmentActor` SHALL
NOT contain type-specific Materialize methods — all dispatch is via the feature
registry.

#### Scenario: Enrichment produces EnrichmentUpdate
- **WHEN** any enrichment feature computes a result for location "lucerne"
- **THEN** it SHALL emit `EgressEvent.EnrichmentUpdate("lucerne", feature.TypeName, result)`
  into the EgressActor's MergeHub

#### Scenario: No type-specific Materialize methods
- **WHEN** the `EnrichmentActor` source file is inspected
- **THEN** it SHALL NOT contain methods named `MaterializeConsensusConsumer`,
  `MaterializeAlertConsumer`, `MaterializeDerivedConsumer`,
  `MaterializeTrendConsumer`, `MaterializeIndexConsumer`,
  `MaterializeEnergyConsumer`, or `MaterializeHistoryConsumer`

#### Scenario: No MQTT dependency
- **WHEN** the `EnrichmentActor` source file is compiled
- **THEN** it SHALL have no `using Njord.Mqtt` directive and no reference to `MqttMessage`, `MqttSinkResponse`, `RequestMqttSink`, or any other `Njord.Mqtt` type

### Requirement: Consumer streams are materialized only when enabled
The `EnrichmentActor` SHALL iterate over all `IEnrichmentFeature` instances
received via DI. For each feature where `Enabled` is `true`, the actor SHALL
materialise the appropriate consumer stream based on the feature's interface
type:
- `IStatelessEnrichment<T>`: `SelectMany(snapshot => feature.Compute(snapshot, locations))`
- `IStatefulEnrichment<T>`: `Scan` to pair current/previous, then `SelectMany(pair => feature.Compute(...))`
- `IActorEnrichment`: delegate to `feature.Materialize(source, sink, mat, context)`

For features where `Enabled` is `false`, no consumer stream SHALL be
materialised.

#### Scenario: Disabled feature is not materialized
- **WHEN** an `IEnrichmentFeature` has `Enabled` set to `false`
- **THEN** no consumer stream is materialised for that feature

#### Scenario: Enabled stateless feature is materialized via loop
- **WHEN** an `IStatelessEnrichment<T>` has `Enabled` set to `true`
- **THEN** a consumer stream is materialised using
  `SelectMany(snapshot => feature.Compute(snapshot, locations))`

#### Scenario: Enabled stateful feature uses Scan pairing
- **WHEN** an `IStatefulEnrichment<T>` has `Enabled` set to `true`
- **THEN** a consumer stream is materialised with a `Scan` operator carrying
  the previous snapshot and calling `feature.Compute(snapshot, previous, locations)`

#### Scenario: Enabled actor feature delegates materialisation
- **WHEN** an `IActorEnrichment` has `Enabled` set to `true`
- **THEN** the actor calls `feature.Materialize(source, sink, mat, context)`
  and does not wire the stream itself

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
The `EnrichmentActor` SHALL delegate history stream materialisation to the
`IActorEnrichment.Materialize` method. The history feature SHALL create
per-location child `ForecastHistoryActor` instances as children of the
`EnrichmentActor`. The stream SHALL use `SelectAsync` for actor queries, not
blocking `.Result`.

#### Scenario: History uses SelectAsync
- **WHEN** the history enrichment queries a `ForecastHistoryActor`
- **THEN** it SHALL use `SelectAsync` with an async lambda

#### Scenario: History actors are children of EnrichmentActor
- **WHEN** the history consumer is enabled with 2 locations
- **THEN** 2 ForecastHistoryActor children exist, one per location

### Requirement: ForecastHistoryActor uses TimeProvider
The `ForecastHistoryActor` SHALL receive `TimeProvider` via constructor
injection and use `timeProvider.GetUtcNow()` for all time operations. It
SHALL NOT use `DateTimeOffset.UtcNow` directly.

#### Scenario: TimeProvider is injected
- **WHEN** `ForecastHistoryActor` computes a retention cutoff
- **THEN** it SHALL use `timeProvider.GetUtcNow()` as the reference time
