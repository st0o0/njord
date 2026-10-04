## MODIFIED Requirements

### Requirement: ModelStateActor owns a BroadcastHub and serves SourceRefs to consumers
The `ModelStateActor` SHALL inherit from `StreamConsumerActor`. It SHALL resolve `PipelineActor` via `GetActorAsync` in its `ResolveDependencies()` override to obtain a single SourceRef for `FetchOutcome`. It SHALL NOT resolve `EgressActor` (eliminated). In its `MaterializeGraph()`, it SHALL process the `FetchOutcome` stream (capability learning, horizon projection, per-model update emission) and feed the output into a `BroadcastHub.Sink<EgressEvent>`. The BroadcastHub source SHALL be stored and used to serve `RequestModelStateSource(long RequestId)` messages from consumers. On request, it SHALL vend a `SourceRef<EgressEvent>` via `StreamRefs.SourceRef<EgressEvent>()` run against the BroadcastHub source. When SourceRef materialization fails, it SHALL send `ModelStateSourceFailed(long RequestId, Exception Cause)` to the requester.

#### Scenario: ModelStateActor subscribes only to PipelineActor
- **WHEN** ModelStateActor starts
- **THEN** it resolves PipelineActor and requests a SourceRef — no EgressActor dependency

#### Scenario: FetchOutcome.Success produces PerModelUpdate on BroadcastHub
- **WHEN** a FetchOutcome.Success arrives via the pipeline SourceRef
- **THEN** the ModelStateActor emits an EgressEvent.PerModelUpdate on its BroadcastHub

#### Scenario: Consumer requests and receives a SourceRef
- **WHEN** MqttStateActor sends `RequestModelStateSource`
- **THEN** ModelStateActor responds with `ModelStateSourceResponse` containing a valid SourceRef

#### Scenario: SourceRef materialization failure sends typed failure
- **WHEN** SourceRef materialization fails
- **THEN** ModelStateActor sends `ModelStateSourceFailed` carrying the exception

#### Scenario: No Ingest namespace import in Egress
- **WHEN** the codebase is compiled
- **THEN** no file under Njord.Egress contains `using Njord.Ingest`

### Requirement: MqttMessage is the unified publish protocol
All messages destined for the broker SHALL be expressed as `MqttMessage(string Topic, string Payload, bool Retain)`. The publish path SHALL be content-agnostic — it does not interpret topic or payload semantics.

#### Scenario: MqttMessage carries topic, payload, and retain flag
- **WHEN** an `MqttMessage("njord/home/icon_d2/state", "{...}", true)` is published
- **THEN** the transport publishes to topic `njord/home/icon_d2/state` with the given payload and retain=true

### Requirement: The Publish Sink buffers during disconnect with DropHead overflow
The Publish Sink SHALL use a bounded buffer (configurable, default 64 messages). When the broker is unreachable and the buffer is full, the oldest messages SHALL be dropped (DropHead strategy). On reconnect, buffered messages SHALL drain in order.

#### Scenario: Short outage buffers and drains
- **WHEN** the broker disconnects for 30 seconds and 10 messages arrive
- **THEN** all 10 are buffered and published in order on reconnect

#### Scenario: Extended outage drops oldest messages
- **WHEN** the broker is down and 100 messages arrive into a 64-message buffer
- **THEN** the newest 64 messages are retained; the oldest 36 are dropped

### Requirement: Availability messages are fed via Source.Queue
The `MqttConnectionActor` SHALL offer an `online` availability MqttMessage after every successful connect, and an `offline` message during graceful shutdown. These flow through the MqttConnectionActor's internal MergeHub.

#### Scenario: Online on connect
- **WHEN** the broker connection succeeds
- **THEN** an `MqttMessage(availabilityTopic, "online", retain: true)` is offered

#### Scenario: Offline on shutdown
- **WHEN** the actor is stopping
- **THEN** an `MqttMessage(availabilityTopic, "offline", retain: true)` is offered

### Requirement: GrpcSnapshotConsumerActor routes events from producer BroadcastHubs to snapshot actors
`GrpcSnapshotConsumerActor` SHALL subscribe to the `ModelStateActor` and `EnrichmentActor` BroadcastHubs via SourceRef. It SHALL NOT subscribe to an `EgressActor` hub. It SHALL merge both sources and route `PerModelUpdate` events to `ForecastSnapshotActor` via Ask/Ack and `EnrichmentUpdate` events to `EnrichmentSnapshotActor` via Ask/Ack. When snapshot actors are ShardRegion entities, the Ask SHALL be sent to the ShardRegion ref with the entity-routable message.

#### Scenario: Forecast update routed with backpressure
- **WHEN** a PerModelUpdate event arrives from ModelStateActor's BroadcastHub
- **THEN** it is sent to ForecastSnapshotActor (or ShardRegion) via Ask and Ack is awaited

#### Scenario: Enrichment update routed with backpressure
- **WHEN** an EnrichmentUpdate event arrives from EnrichmentActor's BroadcastHub
- **THEN** it is sent to EnrichmentSnapshotActor (or ShardRegion) via Ask and Ack is awaited

#### Scenario: Consumer subscribes to two producers
- **WHEN** GrpcSnapshotConsumerActor starts
- **THEN** it resolves ModelStateActor and EnrichmentActor and requests SourceRefs from both

## REMOVED Requirements

### Requirement: MergeHub converges messages from multiple sources into a single publish sink
**Reason**: The central EgressActor hub that merged messages from ModelStateActor and EnrichmentActor into a single MergeHub is eliminated. Each producer owns its own BroadcastHub. MqttConnectionActor retains its internal MergeHub only for availability/tombstone messages — not for data flow.
**Migration**: Consumers (MqttStateActor, MqttDiscoveryActor, GrpcSnapshotConsumerActor) subscribe directly to the producers' BroadcastHubs via SourceRef.

### Requirement: ModelStateActor consumes FetchOutcome from Pipeline SourceRef
**Reason**: Superseded by the expanded requirement above that adds BroadcastHub ownership. The original requirement only covered input; the replacement covers input and output.
**Migration**: No action needed — the replacement includes all original behavior.

### Requirement: Discovery messages are fed via Source.Queue on lifecycle events
**Reason**: Discovery is now handled by MqttDiscoveryActor which calls `IMqttTransport.SendAsync` directly. Discovery messages no longer flow through the MqttConnectionActor's MergeHub.
**Migration**: MqttDiscoveryActor calls `IMqttTransport.SendAsync` for discovery payloads.

### Requirement: Tombstone messages are fed via Source.Queue on stale config detection
**Reason**: Tombstone publishing moves to MqttDiscoveryActor which calls `IMqttTransport.SendAsync` directly.
**Migration**: MqttDiscoveryActor handles tombstone detection and publishes empty payloads via `IMqttTransport.SendAsync`.
