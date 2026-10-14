## MODIFIED Requirements

### Requirement: ForecastReadModel serves unary and streaming RPCs
`ForecastReadModel`, an actor in `Njord.Pipeline` resolved by the `IForecastReadModel` marker, SHALL hold in memory, per location, the latest forecast per model and the most recent analysis event, and SHALL answer `QueryForecast` with `ForecastResult` or `ForecastNotAvailable` and `QueryLatestAnalysis` with `LatestAnalysisResult` or `LatestAnalysisNotAvailable`. It SHALL feed every received event into a secondary `BroadcastHub<PipelineEvent>` (buffer size 2) and SHALL answer `RequestReadModelStream(RequestId)` with `RequestReadModelStreamCompleted` carrying a `SourceRef<PipelineEvent>`, or `RequestReadModelStreamFailed` while the hub is not materialized. The gRPC API project SHALL NOT contain this or any other actor.

The secondary hub SHALL never slow down the read model: it SHALL keep a permanent consumer that discards every event, so the read model keeps consuming the pipeline output whether or not a streaming client is attached. Each streaming client SHALL receive its events through its own bounded buffer of 256 events that drops the oldest buffered event when full, so a client that consumes slowly or not at all loses events for itself only and never holds up the read model, the pipeline or any other consumer.

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

#### Scenario: No streaming client attached
- **WHEN** no gRPC streaming client is attached and the pipeline emits many more events than the secondary hub buffers
- **THEN** `ForecastReadModel` answers queries with the latest of those events, and every other pipeline consumer (such as the MQTT state publisher) receives all of them

#### Scenario: Streaming client that does not consume
- **WHEN** a streaming client holds a SourceRef from `RequestReadModelStream` but does not pull, and the pipeline emits more events than its buffer holds
- **THEN** `ForecastReadModel` and every other pipeline consumer keep receiving all events, and when the client starts pulling it receives at most the 256 most recent events it missed, oldest dropped first

#### Scenario: Streaming client that keeps up
- **WHEN** a streaming client pulls at least as fast as the pipeline emits
- **THEN** it receives every event emitted after it attached, in order, with none dropped
