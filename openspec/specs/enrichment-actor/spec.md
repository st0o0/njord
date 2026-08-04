# enrichment-actor Specification

## Purpose

The EnrichmentActor consumes the pipeline's BroadcastHub via SourceRef, maintains a running ModelSnapshot via Scan, fans out to consumer streams through a second BroadcastHub, and routes computed domain results to the EgressActor as `EgressEvent` variants via StreamRef. Consumer streams are materialized only when enabled in configuration.

## Requirements

### Requirement: The EnrichmentActor requests a SourceRef from the PipelineActor
The `EnrichmentActor` SHALL inherit from `StreamConsumerActor`. It SHALL resolve `PipelineActor`, `EgressActor`, and `SensorHub` via `GetActorAsync` in its `ResolveDependencies()` override. In its `*Resolved` handlers it SHALL call `TrackDependency()` and check `IsDeadRef()`. It SHALL wire the base-provided `SharedKillSwitch.Flow<FetchOutcome>()` into its stream graph in `MaterializeGraph()`. Messages received before all refs arrive SHALL be stashed by the base. The HandleTerminated behavior is fully managed by the `StreamConsumerActor` base: KillSwitch shutdown, dead-ref detection with exponential backoff retry, stale-response gating.

#### Scenario: SourceRef received transitions to operational
- **WHEN** the EnrichmentActor starts and receives PipelineSourceResponse, EgressSinkResponse, and resolves SensorHub
- **THEN** it transitions to its operational state and unstashes pending messages

#### Scenario: Messages are stashed before SourceRef
- **WHEN** the EnrichmentActor receives messages before all refs arrive
- **THEN** the messages are stashed and replayed after the transition

#### Scenario: PipelineActor restart triggers re-request
- **WHEN** the EnrichmentActor receives a `Terminated` message for a tracked dependency
- **THEN** it shuts down the KillSwitch and re-requests with backoff retry

#### Scenario: Peer actors resolved asynchronously
- **WHEN** the EnrichmentActor starts
- **THEN** it resolves PipelineActor, EgressActor, and SensorHub via GetActorAsync, not sync GetActor

### Requirement: EnrichmentActor resolves SensorHub dependency
The EnrichmentActor SHALL resolve the SensorHub actor as an additional dependency alongside PipelineActor and EgressActor. The stream SHALL NOT materialize until all three dependencies are resolved.

#### Scenario: SensorHub resolved
- **WHEN** the SensorHub actor is registered in the ActorRegistry
- **THEN** the EnrichmentActor SHALL resolve it and include it in dependency tracking

#### Scenario: SensorHub unavailable at startup
- **WHEN** the SensorHub actor is not yet available
- **THEN** the EnrichmentActor SHALL retry resolution using the standard retry mechanism

### Requirement: EnrichmentActor pulls SensorSnapshot per location
Before computing enrichments for a location, the EnrichmentActor SHALL send a `GetSnapshot(location)` message to the SensorHub and pass the resulting `SensorSnapshot?` to all enrichment `Compute` calls. The Ask SHALL use a short timeout (1 second). If the SensorHub does not respond, a null snapshot SHALL be used.

#### Scenario: SensorHub responds with data
- **WHEN** the SensorHub has readings for location "Luzern"
- **THEN** the enrichments SHALL receive a non-null `SensorSnapshot` with those readings

#### Scenario: SensorHub responds with no data
- **WHEN** the SensorHub has no readings for location "Luzern"
- **THEN** the enrichments SHALL receive a null `SensorSnapshot`

#### Scenario: SensorHub Ask times out
- **WHEN** the SensorHub does not respond within 1 second
- **THEN** the enrichments SHALL receive a null `SensorSnapshot` and processing SHALL continue

### Requirement: The EnrichmentActor maintains a ModelSnapshot via Scan

The EnrichmentActor SHALL accumulate `FetchOutcome.Success` into a `ModelSnapshot` via Scan. After accumulation, the snapshot SHALL be broadcast to two branches: (1) History (raw `ModelSnapshot`), (2) Consensus transformation followed by enrichments.

#### Scenario: Success updates the snapshot
- **WHEN** a `FetchOutcome.Success` arrives
- **THEN** the `ModelSnapshot` is updated and broadcast to both branches

#### Scenario: Failure does not change the snapshot
- **WHEN** a `FetchOutcome.Failure` arrives
- **THEN** the `ModelSnapshot` remains unchanged

#### Scenario: Unchanged data is filtered
- **WHEN** a `FetchOutcome.Success` arrives with identical data
- **THEN** downstream receives no update

#### Scenario: No Ingest namespace import in Enrichment
- **WHEN** the EnrichmentActor source is inspected
- **THEN** it SHALL NOT import any namespace from `Njord.Ingest`

### Requirement: The EnrichmentActor fans out via a second BroadcastHub
The `EnrichmentActor` SHALL materialize a `BroadcastHub.Sink<ModelSnapshot>` with a buffer size of 8 from the Scan output to distribute rolling snapshots to enrichment features. Consumer streams SHALL each independently subscribe to this BroadcastHub. Each consumer SHALL receive every changed snapshot. The `ModelSnapshot.Update()` method SHALL use `ImmutableDictionary` with structural sharing instead of cloning a mutable `Dictionary` on every update.

#### Scenario: Two consumers receive the same snapshot
- **WHEN** a changed `ModelSnapshot` enters the BroadcastHub and two consumers are subscribed
- **THEN** both consumers receive the snapshot independently

#### Scenario: ModelSnapshot BroadcastHub buffer size is 8
- **WHEN** the EnrichmentActor materializes its enrichment graph
- **THEN** the ModelSnapshot BroadcastHub SHALL use a buffer size of 8

#### Scenario: ModelSnapshot update uses structural sharing
- **WHEN** `ModelSnapshot.Update()` is called with a new forecast
- **THEN** it SHALL return a new `ModelSnapshot` using `ImmutableDictionary.SetItem` without copying the entire dictionary

### Requirement: EnrichmentActor fans out enrichment results to EgressActor

The enrichment inline flow SHALL consume `ConsensusSnapshot` (not `ModelSnapshot`) and produce `EgressEvent` messages sent to the `EgressActor`.

#### Scenario: Enrichment produces EnrichmentUpdate
- **WHEN** a `ConsensusSnapshot` flows through the enrichment inline flow
- **THEN** each enabled enrichment produces `EgressEvent.EnrichmentUpdate`

#### Scenario: No type-specific Materialize methods
- **WHEN** the `EnrichmentActor` source is inspected
- **THEN** there SHALL be no per-enrichment-type materialization methods

#### Scenario: No MQTT dependency
- **WHEN** the `EnrichmentActor` project references are inspected
- **THEN** there SHALL be no reference to MQTTnet

### Requirement: The EnrichmentActor pipeline broadcasts ModelSnapshot before consensus

The stream graph SHALL broadcast the `ModelSnapshot` to two outputs: one for History (raw) and one for the consensus `Select` stage. The consensus stage output SHALL feed the enrichment inline flow.

#### Scenario: History receives raw ModelSnapshot
- **WHEN** a `ModelSnapshot` is broadcast
- **THEN** the History branch receives the unmodified `ModelSnapshot`

#### Scenario: Enrichments receive ConsensusSnapshot
- **WHEN** a `ModelSnapshot` is broadcast
- **THEN** the enrichment branch receives `ConsensusSnapshot` instances produced by the consensus `Select` stage

#### Scenario: Consensus stage is a Select transformation
- **WHEN** the stream graph is inspected
- **THEN** the consensus stage SHALL be a `Select` (map), not an actor or separate materialization

### Requirement: Consumer streams are materialized only when enabled

Enrichment consumer streams SHALL be materialized only for enabled features. The consensus `Select` stage SHALL always be materialized (it is not toggleable). History materialization is unchanged.

#### Scenario: Disabled feature is not materialized
- **WHEN** `EnrichmentOptions.Alerts.Enabled` is false
- **THEN** no alert consumer stream is materialized

#### Scenario: Enabled stateless feature is materialized via loop
- **WHEN** `EnrichmentOptions.Alerts.Enabled` is true
- **THEN** the alert consumer stream is materialized consuming `ConsensusSnapshot`

#### Scenario: Enabled stateful feature uses Scan pairing
- **WHEN** `EnrichmentOptions.Trends.Enabled` is true
- **THEN** the trend consumer uses Scan to pair current and previous `ConsensusSnapshot`

#### Scenario: Enabled actor feature delegates materialisation
- **WHEN** `EnrichmentOptions.History.Enabled` is true
- **THEN** the history consumer is materialized on the raw `ModelSnapshot` branch

### Requirement: Consensus egress events originate from the consensus stage

The consensus stage SHALL emit `EgressEvent.ConsensusUpdate` directly to the `EgressActor`, separate from the enrichment inline flow.

#### Scenario: ConsensusUpdate reaches EgressActor
- **WHEN** the consensus `Select` stage produces `ConsensusSnapshot` instances
- **THEN** corresponding `EgressEvent.ConsensusUpdate` events SHALL be sent to the `EgressActor`

#### Scenario: Consensus egress does not flow through enrichment inline flow
- **WHEN** the enrichment inline flow processes enrichments
- **THEN** it SHALL NOT produce consensus-related `EgressEvent` messages

### Requirement: Enrichment streams sink to EgressActor instead of MergeHub

Each enrichment consumer stream SHALL use `RunWith(egressSinkRef.Sink, mat)` to deliver `EgressEvent` instances to the EgressActor's MergeHub. The stream graphs SHALL NOT maintain their own dedup dictionaries — deduplication is the responsibility of the downstream protocol-specific consumers.

#### Scenario: Consumer graph terminates at EgressActor sink
- **WHEN** an enrichment consumer sub-graph is materialized
- **THEN** its terminal sink SHALL be the `ISinkRef<EgressEvent>` obtained from the EgressActor

#### Scenario: No per-consumer dedup in enrichment
- **WHEN** an enrichment sub-graph produces an `EgressEvent` with the same payload as a previous emission
- **THEN** the enrichment sub-graph SHALL still emit it — dedup is downstream

### Requirement: Stream supervision resumes on consumer errors

The stream supervision strategy SHALL resume on consumer exceptions without killing the pipeline.

#### Scenario: Consumer exception does not kill the pipeline
- **WHEN** an enrichment consumer throws an exception
- **THEN** the stream resumes and other consumers are unaffected

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
