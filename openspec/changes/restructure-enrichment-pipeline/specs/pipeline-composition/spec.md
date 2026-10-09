## ADDED Requirements

### Requirement: PipelineActor owns the full stream graph

`PipelineActor` SHALL materialize the complete stream graph from fetch through
feature fan-out to a `BroadcastHub<PipelineEvent>` endpoint. No intermediate
relay actors (`ModelStateActor`, `EnrichmentActor`), no intermediate
BroadcastHubs, no `ConsensusFlowFactory`.

#### Scenario: Pipeline graph structure
- **WHEN** `PipelineActor` materializes the graph
- **THEN** the graph follows: Fetch → Consensus → Scan (previous) →
  Build PollContext → Broadcast → [Feature Flows] → Merge → KeepAlive →
  Buffer → IdleTimeout → BroadcastHub

#### Scenario: Feature invocation in pipeline
- **WHEN** the pipeline invokes a stateless feature (Alerts, Derived, Trends, Indices)
- **THEN** it calls the feature's compute method via `Flow.Select()` with the
  `PollContext` typed as that feature's input interface

#### Scenario: History invocation in pipeline
- **WHEN** the pipeline invokes the History feature
- **THEN** it uses `Flow.SelectAsync()` with an Ask to the
  `ForecastHistoryActor` for record + query + compute

#### Scenario: PipelineActor is a lifecycle owner only
- **WHEN** the `PipelineActor` is inspected
- **THEN** it contains no business logic and no state beyond graph lifecycle
  management (materialize, keep alive, restart on failure)

### Requirement: PollContext implements all feature input interfaces

The Host SHALL define a `PollContext` record that implements every feature's
input interface (`IAlertInput`, `ITrendInput`, `IIndexInput`, `IHistoryInput`,
`IDerivedInput`). The `PollContext` is assembled after consensus computation
and contains all data any feature might need.

#### Scenario: PollContext construction
- **WHEN** a poll cycle produces a consensus snapshot
- **THEN** the pipeline assembles a `PollContext` with the consensus snapshot,
  sensor data, previous snapshot, and model snapshots

#### Scenario: PollContext interface conformance
- **WHEN** a feature's compute method is called
- **THEN** it receives the `PollContext` typed as its own input interface and
  can access only the data it declared

### Requirement: Trends previous-state via Scan operator

The previous `ConsensusSnapshot` for trend analysis SHALL be maintained by a
`Scan` operator in the pipeline graph. The `TrendAnalyzer` itself is
stateless — it receives current and previous via `ITrendInput`.

#### Scenario: First poll cycle
- **WHEN** the first poll cycle runs
- **THEN** `ITrendInput.Previous` is `null` and the trend analyzer produces no
  trend result

#### Scenario: Subsequent poll cycles
- **WHEN** a poll cycle follows a successful previous cycle
- **THEN** `ITrendInput.Previous` contains the previous cycle's consensus
  snapshot

### Requirement: History runs inline in the pipeline

The `ForecastHistoryActor` SHALL be invoked as part of the pipeline flow (not as
a separate stream branch). Per poll cycle: record the snapshot, query the full
history, compute analysis — all inline before results reach the BroadcastHub.

#### Scenario: History inline execution
- **WHEN** a poll cycle reaches the history stage
- **THEN** the pipeline sends `RecordSnapshot` to the actor, awaits the query
  response, runs `HistoryTracker.Analyze()`, and includes the result in the
  merged output

### Requirement: BroadcastHub with KeepAlive pattern

The pipeline's `BroadcastHub` SHALL use the KeepAlive + Buffer + IdleTimeout
pattern to handle dead consumers without affecting healthy ones. KeepAlive
sends heartbeats during idle periods (between 60-minute poll cycles), the
buffer decouples consumer backpressure from the producer, and IdleTimeout
detects unresponsive consumers.

#### Scenario: Consumer dies
- **WHEN** a consumer (MQTT or gRPC) becomes unresponsive
- **THEN** IdleTimeout detects the dead consumer and cleans up its subscription
  without affecting other consumers

#### Scenario: Idle period between polls
- **WHEN** no poll cycle runs for up to 60 minutes
- **THEN** KeepAlive sends periodic heartbeats to keep the BroadcastHub alive
  and consumer health detection active

### Requirement: Centralized actor registration

All actors (including `ForecastHistoryActor` from `Njord.History`,
`MqttConsumerActor`, `GrpcConsumerActor`) SHALL be registered in a single
`AkkaSetupContainer` in the Host. Feature libraries define actors; the Host
registers them.

#### Scenario: Actor registration location
- **WHEN** the Host's actor setup is inspected
- **THEN** all actor registrations are in one setup container, organized by
  domain in private methods

### Requirement: Njord.Egress is dissolved

`ModelStateActor`, `EgressSetupContainer`, and the `Njord.Egress` project
SHALL be removed. `ModelStateActor`'s function (extracting per-model forecasts
and capabilities from `FetchOutcome`) SHALL be absorbed into the pipeline graph
as inline `Select` stages. `CapabilityTracker` SHALL move to the pipeline.

#### Scenario: No ModelStateActor
- **WHEN** the pipeline produces per-model forecast data
- **THEN** it is emitted as `PipelineEvent.ModelForecast` directly from the
  graph — no `ModelStateActor` relay

#### Scenario: Capability tracking
- **WHEN** the pipeline processes a fetch outcome
- **THEN** model capabilities are extracted inline and emitted as
  `PipelineEvent.ModelCapability`
