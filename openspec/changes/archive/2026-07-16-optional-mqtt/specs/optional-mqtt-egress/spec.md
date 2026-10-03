## ADDED Requirements

### Requirement: MQTT egress is disabled via config flag
When `Njord:Mqtt:Enabled` is `false`, the service SHALL NOT register MQTTnet transport services (`MqttNetPublisher`, `IMqttConnection`, `IMqttTransport`, `MqttEgressTuning`) in DI. It SHALL NOT register `MqttConnectionActor`, `MqttEgressActor`, or `DiscoveryActor` in the actor system. The ingest pipeline, enrichment, `EgressActor` hub, and gRPC consumers SHALL continue to operate normally.

#### Scenario: Service starts without MQTT
- **WHEN** `Njord:Mqtt:Enabled` is `false`
- **THEN** the service starts successfully without connecting to an MQTT broker, and `IMqttTransport` is not resolvable from DI

#### Scenario: Default is MQTT enabled
- **WHEN** no `Njord:Mqtt:Enabled` value is configured
- **THEN** the effective value is `true` and MQTT services and actors are registered normally

#### Scenario: Pipeline operates without MQTT consumer
- **WHEN** MQTT is disabled
- **THEN** `EgressActor` materializes its BroadcastHub, `ModelStateActor` processes forecasts and emits egress events, and `GrpcSnapshotConsumerActor` receives events — no error or backpressure from the absent MQTT consumer

### Requirement: ModelStateActor skips discovery notification when MQTT is disabled
`ModelStateActor` SHALL resolve `DiscoveryActor` only when `Mqtt.Enabled` is `true`. When MQTT is disabled, `_discoveryActor` SHALL be `null` and `ModelCapabilityLearned` messages SHALL NOT be sent.

#### Scenario: ModelStateActor starts with MQTT disabled
- **WHEN** MQTT is disabled
- **THEN** `ModelStateActor` starts without resolving `DiscoveryActor` and processes forecasts normally

#### Scenario: ModelStateActor sends capabilities when MQTT enabled
- **WHEN** MQTT is enabled and a model reports supported parameters
- **THEN** `ModelStateActor` sends `ModelCapabilityLearned` to `DiscoveryActor`
