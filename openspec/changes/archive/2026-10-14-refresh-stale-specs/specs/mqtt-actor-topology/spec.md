## ADDED Requirements

### Requirement: MqttConnection owns the broker connection
The `MqttConnection` actor (src/Njord.Mqtt/MqttConnection.cs, marker `IMqttConnectionActor`) SHALL be registered as a singleton only when `Njord:Mqtt:Enabled` is `true`. It SHALL use the DI-provided `IMqttConnection` and `IMqttTransport` (both `MqttNetPublisher`) and SHALL NOT materialize any stream or MergeHub. It SHALL connect in `PreStart`; the connection SHALL carry a retained Last Will of `offline` on `{BaseTopic}/status`. After a successful connect it SHALL reset its attempt counter, mark MQTT as connected in `NjordHealthMonitor`, subscribe to `{DiscoveryPrefix}/status`, publish a retained `online` on `{BaseTopic}/status` and send `MqttConnected` to every inbound listener. On a failed connect or a lost connection it SHALL reconnect after `MqttEgressTuning.ReconnectDelay` (default 5 s) doubled per attempt, capped at a factor of 64. Actors that register with `SubscribeInbound` SHALL receive every inbound message as `MqttInboundMessage` until they terminate. Downstream actors SHALL publish by calling `IMqttTransport.SendAsync` directly; no SinkRef is vended.

The actor SHALL use the injected `TimeProvider` for all timestamp operations (health state transitions). It SHALL NOT use `DateTimeOffset.UtcNow` directly.

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

### Requirement: MqttStatePublisher maps PipelineEvent to MQTT messages
`MqttStatePublisher` (src/Njord.Mqtt/MqttStatePublisher.cs, marker `IMqttStatePublisher`) SHALL inherit from `StreamConsumerActor` and be registered only when MQTT is enabled. It SHALL resolve `IForecastPipeline`, request a `SourceRef<PipelineEvent>` with `RequestPipelineOutput`, and in `MaterializeGraph` wire the base-provided `SharedKillSwitch` into a graph that maps every `PipelineEvent` variant to retained `MqttMessage` instances (`ForecastReceived` via `HorizonProjection.BuildPerHorizon` and `TopicScheme.HorizonTopic`; the analysis variants via `StatePayloadBuilder`), skips messages whose payload hash equals the last one published on the same topic, and calls `IMqttTransport.SendAsync` directly in a `SelectAsync(1)` stage. It SHALL NOT resolve `IMqttConnectionActor`.

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

## REMOVED Requirements

### Requirement: MqttConnectionActor owns the broker connection and MergeHub
**Reason**: The requirement named types, files or behavior that no longer exist; it is restated against the current code.
**Migration**: Superseded by "MqttConnection owns the broker connection" in this capability.

### Requirement: MqttStateActor maps EgressEvent to MQTT messages
**Reason**: The requirement named types, files or behavior that no longer exist; it is restated against the current code.
**Migration**: Superseded by "MqttStatePublisher maps PipelineEvent to MQTT messages" in this capability.

### Requirement: MqttDiscoveryActor publishes HA discovery configs
**Reason**: MQTT Discovery was removed; there is no `MqttDiscoveryActor`, no capability events and no discovery payloads.
**Migration**: None; MQTT publishes state and availability only.
