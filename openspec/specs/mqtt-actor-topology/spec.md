# mqtt-actor-topology Specification

## Purpose

Actor topology for MQTT in `Njord.Mqtt`: `MqttConnection` owns the broker connection, reconnects and availability, and `MqttStatePublisher` consumes the pipeline output via SourceRef and maps each `PipelineEvent` to MQTT state messages.
## Requirements
### Requirement: MqttConnection owns the broker connection
The `MqttConnection` actor (src/Njord.Mqtt/MqttConnection.cs, marker `IMqttConnectionActor`) SHALL be registered as a singleton only when `Njord:Mqtt:Enabled` is `true`. It SHALL use the DI-provided `IMqttConnection` and `IMqttTransport` (both `MqttNetPublisher`) and SHALL NOT materialize any stream or MergeHub. It SHALL connect in `PreStart`; the connection SHALL carry a retained Last Will of `offline` on `{BaseTopic}/status`. A CONNECT the broker refuses (non-success CONNACK) SHALL count as a failed connect. After a successful connect it SHALL reset its attempt counter, mark MQTT as connected in `NjordHealthMonitor`, send `MqttConnected` to every inbound listener, subscribe to `{DiscoveryPrefix}/status` and publish a retained `online` on `{BaseTopic}/status`. On a failed connect or a lost connection it SHALL reconnect after `MqttEgressTuning.ReconnectDelay` (default 5 s) doubled per attempt, capped at a factor of 64; at most one reconnect SHALL be pending at a time, and a disconnect signal received while no connection is established SHALL NOT schedule another one. Actors that register with `SubscribeInbound` SHALL receive every inbound message as `MqttInboundMessage` until they terminate. Downstream actors SHALL publish by calling `IMqttTransport.SendAsync` directly; no SinkRef is vended.

No transport failure SHALL crash, restart or block the actor: every connect, subscribe and availability publish SHALL complete as a message the actor handles. A failed status-topic subscribe SHALL be logged as a Warning and keep the connection; it is repeated with the next connect. The `online` publish SHALL be bounded by `MqttEgressTuning.PublishTimeout` (default 10 s); when it throws, faults or times out, the actor SHALL log a Warning, keep the connection and publish `online` again on the same connection after `ReconnectDelay` doubled per failed attempt (capped at a factor of 64) until it succeeds or the connection is lost. On stop, the `offline` publish SHALL be best effort: its failure SHALL be logged and SHALL NOT delay or fail the stop.

The actor SHALL use the injected `TimeProvider` for all timestamp operations (health state transitions). It SHALL NOT use `DateTimeOffset.UtcNow` directly. When an established connection is lost it SHALL mark MQTT as disconnected with the time of the loss and SHALL keep that timestamp while reconnect attempts fail.

#### Scenario: Connection established
- **WHEN** the actor connects to the broker
- **THEN** it publishes a retained `online` on the availability topic

#### Scenario: Actor not registered when MQTT disabled
- **WHEN** `Njord:Mqtt:Enabled` is false
- **THEN** the actor is not registered in the actor system

#### Scenario: Connection lost and reconnected
- **WHEN** the broker connection is lost
- **THEN** the actor marks MQTT as disconnected and reconnects with exponential backoff

#### Scenario: Health timestamps use TimeProvider
- **WHEN** the actor records a disconnect timestamp
- **THEN** it uses `TimeProvider.GetUtcNow()` instead of `DateTimeOffset.UtcNow`

#### Scenario: Graceful shutdown publishes offline
- **WHEN** the actor stops
- **THEN** it publishes a retained `offline` on the availability topic

#### Scenario: Failed online publish is retried on the same connection
- **WHEN** the `online` publish after a connect fails
- **THEN** the actor logs a Warning, keeps the connection without reconnecting, MQTT stays marked as connected, and `online` is published again until it succeeds

#### Scenario: Failed online publish does not restart the actor
- **WHEN** the `online` publish after a reconnect fails
- **THEN** the actor is not restarted: no `offline` is published, registered inbound listeners keep receiving inbound messages and no further connect is attempted on the established connection

#### Scenario: Hung online publish times out
- **WHEN** the `online` publish does not complete within the publish timeout
- **THEN** it counts as failed, the actor keeps handling messages and publishes `online` again

#### Scenario: Failed status subscription keeps the connection
- **WHEN** subscribing to the Home Assistant status topic fails
- **THEN** the actor logs a Warning, keeps the connection and still publishes `online`

#### Scenario: Failed connect schedules exactly one reconnect
- **WHEN** a connect fails and the client also signals a disconnect for that attempt
- **THEN** exactly one reconnect is scheduled for that failure

#### Scenario: Refused connect is a failed connect
- **WHEN** the broker answers the CONNECT with a non-success result
- **THEN** the connect fails and the actor reconnects with backoff instead of treating the connection as established

#### Scenario: Disconnect timestamp survives failed reconnects
- **WHEN** an established connection is lost and the following reconnect attempts fail
- **THEN** the MQTT health snapshot keeps the time of the loss as its disconnect timestamp

#### Scenario: Failed offline publish does not block the stop
- **WHEN** the actor stops and the `offline` publish fails
- **THEN** the actor stops and the failure is logged

### Requirement: MqttStatePublisher maps PipelineEvent to MQTT messages
`MqttStatePublisher` (src/Njord.Mqtt/MqttStatePublisher.cs, marker `IMqttStatePublisher`) SHALL inherit from `StreamConsumerActor` and be registered only when MQTT is enabled. Its subscription to the pipeline output (see `stream-consumer-actor`) SHALL run a private graph that first passes every `PipelineEvent` through a bounded buffer of `MqttEgressTuning.EventBufferSize` events (default 256) that drops the oldest buffered event when full, then maps every `PipelineEvent` variant to retained `MqttMessage` instances (`ForecastReceived` via `HorizonProjection.BuildPerHorizon` and `TopicScheme.HorizonTopic`; the analysis variants via `StatePayloadBuilder`), and finally publishes one message at a time (`Sink.ForEachAsync` with parallelism 1): a message whose payload hash equals the last one published on the same topic is skipped, every other message is sent with `IMqttTransport.SendAsync` directly. It SHALL NOT resolve `IMqttConnectionActor`. A slow or stuck broker SHALL never back-pressure the pipeline output: while publishing lags behind, the publisher keeps pulling events and drops its own oldest buffered ones.

A failed publish SHALL affect only the message being published: when `IMqttTransport.SendAsync` throws, faults, or does not complete within `MqttEgressTuning.PublishTimeout` (default 10 s), the publisher SHALL log a Warning naming the topic, count the failure in `njord_mqtt_publish_failures_total`, drop that message and continue with the next one. A failed publish SHALL NOT fail the graph and SHALL NOT cancel the pipeline subscription.

#### Scenario: Publisher subscribes to the pipeline
- **WHEN** `MqttStatePublisher` starts
- **THEN** it requests a `SourceRef<PipelineEvent>` from `ForecastPipeline`

#### Scenario: ForecastReceived becomes one message per horizon
- **WHEN** MQTT is enabled and a `ForecastReceived` arrives
- **THEN** one retained message per horizon with values is published on `{baseTopic}/{location}/{model}/{horizon}`

#### Scenario: Actor not registered when MQTT disabled
- **WHEN** `Njord:Mqtt:Enabled` is `false`
- **THEN** `MqttStatePublisher` is not registered in the actor system

#### Scenario: Publisher deduplicates by topic
- **WHEN** a mapped message has the same payload hash as the last message published on its topic
- **THEN** it is skipped

#### Scenario: Publisher calls transport directly
- **WHEN** `MqttStatePublisher` has a message to publish
- **THEN** it calls `IMqttTransport.SendAsync` directly

#### Scenario: Later events are published after a failed publish
- **WHEN** a publish fails (e.g. the broker is not connected) and a later `PipelineEvent` arrives once the transport works again
- **THEN** the later event's messages are published
- **AND** a Warning naming the failed topic is logged

#### Scenario: Failed publish keeps the pipeline subscription
- **WHEN** a publish fails
- **THEN** the `SourceRef<PipelineEvent>` subscription stays open and the pipeline output is not cancelled

#### Scenario: Hung publish times out
- **WHEN** `IMqttTransport.SendAsync` does not complete within the publish timeout
- **THEN** the publish counts as failed and the next message is published

#### Scenario: A stuck broker does not back-pressure the pipeline
- **WHEN** a publish never completes within the test and the pipeline output emits many more events than the publisher's buffer holds
- **THEN** another consumer of the same pipeline output receives every event, and the publisher keeps only its most recent buffered events

