## ADDED Requirements

### Requirement: ForecastReadModel serves unary and streaming RPCs
`ForecastReadModel`, an actor in `Njord.Pipeline` resolved by the `IForecastReadModel` marker, SHALL hold in memory, per location, the latest forecast per model and the most recent analysis event, and SHALL answer `QueryForecast` with `ForecastResult` or `ForecastNotAvailable` and `QueryLatestAnalysis` with `LatestAnalysisResult` or `LatestAnalysisNotAvailable`. It SHALL feed every received event into a secondary `BroadcastHub<PipelineEvent>` (buffer size 2) and SHALL answer `RequestReadModelStream(RequestId)` with `RequestReadModelStreamCompleted` carrying a `SourceRef<PipelineEvent>`, or `RequestReadModelStreamFailed` while the hub is not materialized. The gRPC API project SHALL NOT contain this or any other actor.

#### Scenario: Unary query
- **WHEN** a gRPC client calls `GetEnrichments` for a location
- **THEN** the gRPC service asks `ForecastReadModel` with `QueryLatestAnalysis` and maps the typed reply; no persistence, no shard region

#### Scenario: Unary query after restart
- **WHEN** `ForecastReadModel` restarts and a client queries before the first forecast is processed
- **THEN** it replies `ForecastNotAvailable` or `LatestAnalysisNotAvailable`

#### Scenario: Streaming RPC
- **WHEN** a gRPC client calls `StreamForecasts` or `StreamEnrichments`
- **THEN** the gRPC service asks `ForecastReadModel` with `RequestReadModelStream` and streams the filtered `PipelineEvent` instances from the returned SourceRef

#### Scenario: gRPC streaming client disconnects
- **WHEN** a gRPC streaming client cancels its call
- **THEN** the call's cancellation token shuts down the shared kill switch of that call's stream, and other streaming clients are not affected

### Requirement: Consumer registration via setup containers
Each consumer SHALL be registered by the setup container of the library that owns it: `MqttSetupContainer` registers the MQTT transport services and, through an `IActorRegistration`, the `MqttConnection` and `MqttStatePublisher` singletons only when `Njord:Mqtt:Enabled` is `true`; `PipelineSetupContainer` registers the `ForecastReadModel` singleton. There SHALL be no `AddMqttConsumer()` or `AddGrpcConsumer()` extension methods.

#### Scenario: Enabling MQTT consumer
- **WHEN** the host starts with `Njord:Mqtt:Enabled=true`
- **THEN** `MqttSetupContainer` registers `MqttNetPublisher`, `IMqttConnection`, `IMqttTransport` and the `IMqttConnectionActor` and `IMqttStatePublisher` singletons

#### Scenario: MQTT disabled
- **WHEN** `Njord:Mqtt:Enabled` is `false` or absent
- **THEN** no MQTT transport service and no MQTT actor is registered, and `ForecastReadModel` still attaches to the pipeline

## MODIFIED Requirements

### Requirement: Consumers are output adapters for typed results
Each consumer SHALL receive typed `PipelineEvent` instances (`ForecastReceived`, `ConsensusComputed`, `AlertComputed`, `DerivedComputed`, `TrendComputed`, `IndexComputed`) and translate them into its protocol-specific output format by pattern matching on the variant. Consumers SHALL NOT define presentation abstractions (no `IEnrichmentPresenter`, no `TypeName` matching, no `object` casts).

#### Scenario: MQTT consumer receives alert results
- **WHEN** the pipeline emits a `PipelineEvent.AlertComputed`
- **THEN** `MqttStatePublisher` maps it with `StatePayloadBuilder.FromAlerts` to retained state messages and publishes them — no presenter indirection

#### Scenario: gRPC consumer receives analysis results
- **WHEN** a `ConsensusComputed`, `AlertComputed`, `DerivedComputed`, `TrendComputed` or `IndexComputed` event arrives at `ForecastReadModel`
- **THEN** it updates its in-memory state for the event's location

## REMOVED Requirements

### Requirement: GrpcConsumerActor serves unary and streaming RPCs
**Reason**: The requirement named types, files or behavior that no longer exist; it is restated against the current code.
**Migration**: Superseded by "ForecastReadModel serves unary and streaming RPCs" in this capability.

### Requirement: EnrichmentSnapshotActor shard region is removed
**Reason**: The removal is complete; a requirement that only asserts the absence of `EnrichmentSnapshotActor`, `UpdateEnrichment` and `IWithEnrichmentKey` no longer describes behavior.
**Migration**: None. `ForecastReadModel` (requirement "ForecastReadModel serves unary and streaming RPCs") is the read side.

### Requirement: Consumer registration via DI
**Reason**: The requirement named types, files or behavior that no longer exist; it is restated against the current code.
**Migration**: Superseded by "Consumer registration via setup containers" in this capability.
