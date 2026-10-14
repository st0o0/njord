## MODIFIED Requirements

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
