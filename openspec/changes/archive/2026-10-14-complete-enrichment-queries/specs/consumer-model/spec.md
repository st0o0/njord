## MODIFIED Requirements

### Requirement: ForecastReadModel serves unary and streaming RPCs
`ForecastReadModel`, an actor in `Njord.Pipeline` resolved by the `IForecastReadModel` marker, SHALL hold in memory, per location, the latest forecast per model and the latest analysis event of each analysis kind (consensus, alerts, derived, trends, indices), so a newer event of one kind never replaces the latest event of another kind. It SHALL answer `QueryForecast` with `ForecastResult` or `ForecastNotAvailable`, and `QueryLatestAnalysis` with `LatestAnalysisResult` carrying the latest event of every kind received for the location (a kind without an event is absent), or `LatestAnalysisNotAvailable` while no analysis event of the location has arrived. It SHALL feed every received event into a secondary `BroadcastHub<PipelineEvent>` (buffer size 2) and SHALL answer `RequestReadModelStream(RequestId)` with `RequestReadModelStreamCompleted` carrying a `SourceRef<PipelineEvent>`, or `RequestReadModelStreamFailed` while the hub is not materialized. The gRPC API project SHALL NOT contain this or any other actor.

The secondary hub SHALL never slow down the read model: it SHALL keep a permanent consumer that discards every event, so the read model keeps consuming the pipeline output whether or not a streaming client is attached. Each streaming client SHALL receive its events through its own bounded buffer of 256 events that drops the oldest buffered event when full, so a client that consumes slowly or not at all loses events for itself only and never holds up the read model, the pipeline or any other consumer.

#### Scenario: Unary query
- **WHEN** a gRPC client calls `GetEnrichments` for a location
- **THEN** the gRPC service asks `ForecastReadModel` with `QueryLatestAnalysis` and maps the typed reply; no persistence, no shard region

#### Scenario: Latest event of every analysis kind is kept
- **WHEN** a consensus, an alert, a derived, a trend and an index event arrive for "lucerne", in that order
- **THEN** `QueryLatestAnalysis(lucerne)` answers `LatestAnalysisResult` carrying all five events, not only the index event

#### Scenario: A newer event replaces only its own kind
- **WHEN** an alert event computed at 06:00 and a trend event arrive for "lucerne", followed by an alert event computed at 07:00
- **THEN** the result carries the 07:00 alert event and still the trend event

#### Scenario: Analysis kinds are kept per location
- **WHEN** an alert event arrives for "lucerne" and a trend event for "zurich"
- **THEN** the result for "lucerne" carries only the alert event and the result for "zurich" only the trend event

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
