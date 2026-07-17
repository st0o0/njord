# optional-mqtt-egress Specification

## Purpose

Controls whether MQTT egress is active. When disabled via configuration, the service operates without an MQTT broker connection while the ingest pipeline, enrichment, and gRPC consumers continue normally.

## Requirements

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
