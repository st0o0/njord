## MODIFIED Requirements

### Requirement: MqttConnectionActor owns the broker connection and MergeHub
The `MqttConnectionActor` SHALL be registered in the actor system only when `Mqtt.Enabled` is `true`. When registered, it SHALL own the `IMqttConnection` and `IMqttTransport` instances. It SHALL materialize a MergeHub sink for its own internal outbound `MqttMessage` flow (availability messages, internally-queued discovery tombstones). It SHALL handle connect, reconnect with exponential backoff, LWT (online/offline on the availability topic), and disconnection recovery. It SHALL NOT vend `SinkRef<MqttMessage>` to external requestors — the `RequestMqttSink`/`MqttSinkResponse` protocol is removed. Downstream actors (MqttStateActor, MqttDiscoveryActor) SHALL call `IMqttTransport.SendAsync` directly via DI-injected transport.

The actor SHALL use the injected `TimeProvider` for all timestamp operations (health state transitions). It SHALL NOT use `DateTimeOffset.UtcNow` directly.

#### Scenario: Connection established
- **WHEN** the actor connects to the broker
- **THEN** it publishes "online" on the availability topic

#### Scenario: Actor not registered when MQTT disabled
- **WHEN** Mqtt.Enabled is false
- **THEN** the actor is not registered in the actor system

#### Scenario: Connection lost and reconnected
- **WHEN** the broker connection is lost
- **THEN** the actor reconnects with exponential backoff

#### Scenario: No SinkRef vended to external actors
- **WHEN** any actor sends a request for a SinkRef
- **THEN** MqttConnectionActor does not handle it — the SinkRef API is removed

#### Scenario: Health timestamps use TimeProvider
- **WHEN** the actor records a connect or disconnect timestamp
- **THEN** it uses `TimeProvider.GetUtcNow()` instead of `DateTimeOffset.UtcNow`

#### Scenario: Graceful shutdown publishes offline
- **WHEN** the actor stops
- **THEN** it publishes "offline" on the availability topic

### Requirement: MqttStateActor maps EgressEvent to MQTT messages
The `MqttStateActor` (renamed from `MqttEgressActor`) SHALL inherit from `StreamConsumerActor`. It SHALL resolve `ModelStateActor` and `EnrichmentActor` via `GetActorAsync` in its `ResolveDependencies()` override to obtain SourceRefs from both producers. It SHALL NOT resolve `EgressActor` (eliminated) or `MqttConnectionActor` (no SinkRef needed). In its `*Resolved` handlers it SHALL call `TrackDependency()` and check `IsDeadRef()`. It SHALL wire the base-provided `SharedKillSwitch.Flow<EgressEvent>()` into its stream graph in `MaterializeGraph()`. It SHALL merge the two SourceRef inputs, map all `EgressEvent` variants to `MqttMessage` instances via presenters and `TopicScheme`, deduplicate by topic hash, and call `IMqttTransport.SendAsync` directly in a `SelectAsync(1)` stage. The HandleTerminated behavior is fully managed by the `StreamConsumerActor` base.

#### Scenario: MqttStateActor subscribes to ModelStateActor and EnrichmentActor
- **WHEN** MqttStateActor starts
- **THEN** it requests SourceRefs from both ModelStateActor and EnrichmentActor

#### Scenario: MqttStateActor maps PerModelUpdate to MQTT messages
- **WHEN** MQTT is enabled and `MqttStateActor` receives an `EgressEvent.PerModelUpdate`
- **THEN** it SHALL create one retained `MqttMessage` per horizon entry using `TopicScheme.HorizonTopic` and send via `IMqttTransport.SendAsync`

#### Scenario: Actor not registered when MQTT disabled
- **WHEN** `Mqtt.Enabled` is `false`
- **THEN** `MqttStateActor` is not registered in the actor system

#### Scenario: MqttStateActor deduplicates by topic
- **WHEN** `MqttStateActor` maps an `EgressEvent` to an `MqttMessage` whose topic+payload hash is identical to the last published message on that topic
- **THEN** it SHALL skip publishing that message

#### Scenario: MqttStateActor calls transport directly
- **WHEN** `MqttStateActor` has a message to publish
- **THEN** it calls `IMqttTransport.SendAsync` directly, not through a SinkRef to MqttConnectionActor

### Requirement: MqttDiscoveryActor publishes HA discovery configs
The `MqttDiscoveryActor` (renamed from `DiscoveryActor`) SHALL inherit from `StreamConsumerActor`. It SHALL resolve `ModelStateActor` via `GetActorAsync` in its `ResolveDependencies()` override to obtain a SourceRef for `CapabilityLearned` events. It SHALL NOT resolve `EgressActor` (eliminated) or `MqttConnectionActor` (no SinkRef needed). It SHALL subscribe to `MqttConnectionActor` for inbound HA birth messages via `SubscribeInbound`. It SHALL call `IMqttTransport.SendAsync` directly for discovery payload publishing. All discovery business logic (deferred publishing, HA birth re-publish, capability learning, capability timeout) is unchanged.

#### Scenario: MqttDiscoveryActor subscribes to ModelStateActor
- **WHEN** the actor starts
- **THEN** it requests a SourceRef from ModelStateActor for CapabilityLearned events

#### Scenario: Discovery deferred until capabilities learned
- **WHEN** the actor starts
- **THEN** it waits for capability events before publishing discovery

#### Scenario: All capabilities received triggers discovery
- **WHEN** all expected location/model pairs report capabilities
- **THEN** retained discovery config payloads are published via `IMqttTransport.SendAsync`

#### Scenario: Discovery re-published on HA birth
- **WHEN** "online" is received on the HA status topic via `MqttInboundMessage`
- **THEN** all discovery config payloads are re-published

#### Scenario: Actor not registered when MQTT disabled
- **WHEN** Mqtt.Enabled is false
- **THEN** the actor is not registered

#### Scenario: Timeout triggers partial discovery
- **WHEN** the capability timeout expires with incomplete reports
- **THEN** discovery is published for the capabilities received so far

#### Scenario: MqttDiscoveryActor calls transport directly
- **WHEN** MqttDiscoveryActor publishes a discovery payload
- **THEN** it calls `IMqttTransport.SendAsync` directly, not through a SinkRef

## RENAMED Requirements

### Requirement: MqttEgressActor maps EgressEvent to MQTT messages
- **FROM:** MqttEgressActor
- **TO:** MqttStateActor

### Requirement: DiscoveryActor publishes HA discovery configs
- **FROM:** DiscoveryActor
- **TO:** MqttDiscoveryActor
