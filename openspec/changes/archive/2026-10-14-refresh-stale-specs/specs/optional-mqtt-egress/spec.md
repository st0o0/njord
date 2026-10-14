## MODIFIED Requirements

### Requirement: MQTT egress is disabled via config flag
When `Njord:Mqtt:Enabled` is `false`, `MqttSetupContainer` SHALL NOT register the MQTTnet transport services (`MqttNetPublisher`, `IMqttConnection`, `IMqttTransport`, `MqttEgressTuning`) or the MQTT actor registration, so neither `MqttConnection` nor `MqttStatePublisher` exists in the actor system, and `CoreSetupContainer` SHALL NOT register the `mqtt-connection` health check. The gateway, `ForecastPipeline`, `ForecastReadModel` and the gRPC API SHALL continue to operate normally.

#### Scenario: Service starts without MQTT
- **WHEN** `Njord:Mqtt:Enabled` is `false`
- **THEN** the service starts without connecting to an MQTT broker, and `IMqttTransport` is not resolvable from DI

#### Scenario: Default is MQTT disabled
- **WHEN** no `Njord:Mqtt:Enabled` value is configured
- **THEN** the effective value is `false` and MQTT services and actors are not registered

#### Scenario: Pipeline operates without MQTT consumer
- **WHEN** MQTT is disabled
- **THEN** `ForecastPipeline` materializes its BroadcastHub, emits `PipelineEvent`s, and `ForecastReadModel` receives them — no error or backpressure from the absent MQTT consumer
