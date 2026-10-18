# consumer-model Specification

## Purpose

Consumers of the analysis pipeline: output adapters (the `ForecastReadModel` behind the gRPC API and the state-only `MqttStatePublisher`) attach to the `ForecastPipeline` BroadcastHub via SourceRef, consume typed `PipelineEvent` variants, and are registered through their setup containers.
## Requirements
### Requirement: Consumers attach via SourceRef to pipeline BroadcastHub

Each consumer SHALL request a `SourceRef<PipelineEvent>` from the forecast pipeline and run its own sink. There is exactly one hop from pipeline to consumer: no intermediate relay actors, no intermediate BroadcastHubs between the pipeline and a consumer. A consumer SHALL request a SourceRef only when it starts, when the pipeline incarnation it is attached to terminates, and when its own graph completes or fails; producers attaching, detaching or restarting SHALL NOT make it re-attach. No consumer SHALL slow down the pipeline's output: each consumer SHALL keep pulling from its SourceRef whatever its own output does, dropping its own oldest events when its output falls behind.

#### Scenario: MQTT consumer attaches
- **WHEN** the MQTT state publisher starts
- **THEN** it requests a `SourceRef<PipelineEvent>` from the forecast pipeline and
  materializes its own sink that publishes state to MQTT

#### Scenario: gRPC consumer attaches
- **WHEN** `ForecastReadModel` starts
- **THEN** it requests a `SourceRef<PipelineEvent>` from the forecast pipeline and
  materializes its own sink that updates in-memory state and feeds its
  secondary BroadcastHub

#### Scenario: Consumer independence
- **WHEN** any consumer project is inspected
- **THEN** it does not reference other consumer projects; consumers are
  independent output adapters

#### Scenario: Gateway restart leaves consumers attached
- **WHEN** the gateway restarts while `ForecastReadModel` is attached to the pipeline
- **THEN** the read model sends no further SourceRef request and its state is updated by the forecasts fetched after the restart

#### Scenario: A stuck broker does not hold up other consumers
- **WHEN** MQTT publishes never complete and the pipeline emits many more events than every buffer between the pipeline and the MQTT publisher holds
- **THEN** another consumer of the pipeline output receives every one of those events

### Requirement: Consumers are output adapters for typed results
Each consumer SHALL receive typed `PipelineEvent` instances (`ForecastReceived`, `ConsensusComputed`, `AlertComputed`, `DerivedComputed`, `TrendComputed`, `IndexComputed`) and translate them into its protocol-specific output format by pattern matching on the variant. Consumers SHALL NOT define presentation abstractions (no `IEnrichmentPresenter`, no `TypeName` matching, no `object` casts).

#### Scenario: MQTT consumer receives alert results
- **WHEN** the pipeline emits a `PipelineEvent.AlertComputed`
- **THEN** `MqttStatePublisher` maps it with `StatePayloadBuilder.FromAlerts` to retained state messages and publishes them — no presenter indirection

#### Scenario: gRPC consumer receives analysis results
- **WHEN** a `ConsensusComputed`, `AlertComputed`, `DerivedComputed`, `TrendComputed` or `IndexComputed` event arrives at `ForecastReadModel`
- **THEN** it updates its in-memory state for the event's location

### Requirement: MQTT consumer publishes state only

The MQTT consumer SHALL publish state messages to MQTT topics. It SHALL NOT
manage MQTT Discovery lifecycle (no retained config payloads, no HA birth-message
handling, no tombstoning, no `DiscoveryPayloadBuilder`).

#### Scenario: State publishing
- **WHEN** a poll cycle completes with analysis results
- **THEN** the MQTT consumer publishes state JSON to per-device state topics

#### Scenario: No discovery payloads
- **WHEN** the MQTT consumer starts or receives a HA birth message
- **THEN** it does NOT publish any retained config payloads to
  `homeassistant/device/*/config` topics

### Requirement: ForecastReadModel serves unary and streaming RPCs
`ForecastReadModel`, an actor in `Njord.Pipeline` resolved by the `IForecastReadModel` marker, SHALL hold in memory, per location, the latest forecast per model and the latest analysis event of each analysis kind (consensus, alerts, derived, trends, indices), so a newer event of one kind never replaces the latest event of another kind. It SHALL answer `QueryForecast` with `ForecastResult` or `ForecastNotAvailable`, and `QueryLatestAnalysis` with `LatestAnalysisResult` carrying the latest event of every kind received for the location (a kind without an event is absent), or `LatestAnalysisNotAvailable` while no analysis event of the location has arrived. It SHALL feed every received event into a secondary `BroadcastHub<PipelineEvent>` (buffer size 2), materialized once when the actor starts, and SHALL answer `RequestReadModelStream(RequestId)` at any time, also before its first pipeline subscription, with `RequestReadModelStreamCompleted` carrying a `SourceRef<PipelineEvent>`, or `RequestReadModelStreamFailed` when the SourceRef cannot be materialized. The gRPC API project SHALL NOT contain this or any other actor.

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

#### Scenario: Stream requested before the first subscription
- **WHEN** a `RequestReadModelStream` reaches `ForecastReadModel` before it has received a SourceRef from the pipeline
- **THEN** it answers `RequestReadModelStreamCompleted`, and the client receives the events of the subscription that follows

### Requirement: Consumer registration via setup containers
Each consumer SHALL be registered by the setup container of the library that owns it: `MqttSetupContainer` registers the MQTT transport services and, through an `IActorRegistration`, the `MqttConnection` and `MqttStatePublisher` singletons only when `Njord:Mqtt:Enabled` is `true`; `PipelineSetupContainer` registers the `ForecastReadModel` singleton. There SHALL be no `AddMqttConsumer()` or `AddGrpcConsumer()` extension methods.

#### Scenario: Enabling MQTT consumer
- **WHEN** the host starts with `Njord:Mqtt:Enabled=true`
- **THEN** `MqttSetupContainer` registers `MqttNetPublisher`, `IMqttConnection`, `IMqttTransport` and the `IMqttConnectionActor` and `IMqttStatePublisher` singletons

#### Scenario: MQTT disabled
- **WHEN** `Njord:Mqtt:Enabled` is `false` or absent
- **THEN** no MQTT transport service and no MQTT actor is registered, and `ForecastReadModel` still attaches to the pipeline

### Requirement: Read-model queries match locations case-insensitively
`ForecastReadModel` SHALL match the location of `QueryForecast` and `QueryLatestAnalysis` against the stored locations case-insensitively, as it already does for model ids, so a query spelled differently from the configured location returns the same data as one spelled exactly.

#### Scenario: Forecast query in another spelling
- **WHEN** a forecast for (lucerne, icon_d2) has been received and `QueryForecast(LUCERNE, "icon_d2")` arrives
- **THEN** the read model replies `ForecastResult` with that forecast

#### Scenario: Analysis query in another spelling
- **WHEN** an alert event for `lucerne` has been received and `QueryLatestAnalysis(LUCERNE)` arrives
- **THEN** the read model replies `LatestAnalysisResult` carrying that alert event

### Requirement: Consumer state across a rewire
When a consumer re-attaches after its pipeline subscription ended (see `stream-consumer-actor`), `ForecastReadModel` SHALL keep its in-memory latest values and SHALL keep its secondary hub, so queries answered before the re-attach are answered the same way during and after it, and gRPC streaming clients attached before the re-attach keep their stream and receive the events of the new subscription. `MqttStatePublisher` SHALL start each subscription with an empty dedup cache, and SHALL empty it whenever the broker connection is re-established (`MqttConnectionState.Session` changed), so the first event of every state topic after a re-attach or a reconnect is published even if its payload equals the last published one (a restarted broker may have lost its retained messages).

#### Scenario: Read model keeps its state across a rewire
- **WHEN** `ForecastReadModel` holds the latest alert event for "lucerne" and its pipeline subscription fails
- **THEN** `QueryLatestAnalysis(lucerne)` still answers with that alert event while and after the subscription is re-attached, and events of the new subscription update it

#### Scenario: Streaming client survives a rewire
- **WHEN** a gRPC streaming client is attached to `ForecastReadModel` and the pipeline subscription completes
- **THEN** the client's stream does not end, and it receives the events of the new subscription

#### Scenario: MQTT republishes after a rewire
- **WHEN** `MqttStatePublisher` published a state payload, its subscription failed, and the same payload arrives on the new subscription
- **THEN** the payload is published again

#### Scenario: MQTT republishes after a reconnect
- **WHEN** `MqttStatePublisher` published a state payload, the broker connection was re-established, and the same payload arrives again
- **THEN** the payload is published again

#### Scenario: MQTT does not republish on the same connection
- **WHEN** `MqttStatePublisher` published a state payload and the same payload arrives again on the same subscription and broker connection
- **THEN** the payload is not published again

