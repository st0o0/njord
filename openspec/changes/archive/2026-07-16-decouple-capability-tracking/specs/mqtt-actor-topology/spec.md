## MODIFIED Requirements

### Requirement: DiscoveryActor publishes HA discovery configs
The `DiscoveryActor` SHALL be registered in the actor system only when `Mqtt.Enabled` is `true`. When registered, it SHALL request a `SinkRef<MqttMessage>` from `MqttConnectionActor` and a `SourceRef<EgressEvent>` from `EgressActor`. It SHALL materialize the egress source stream, filtering for `EgressEvent.CapabilityLearned` events and piping them to itself. It SHALL subscribe to the HA status topic (`{discoveryPrefix}/status`) via `MqttConnectionActor`. It SHALL NOT publish discovery on initial connection. Instead, it SHALL collect capability events from the egress hub. Once all expected (location, model) pairs have reported — or a configurable timeout expires (default: 2x poll interval) — it SHALL publish retained discovery config payloads for all reported devices using `DiscoveryPayloadBuilder`, filtered by each model's supported parameters and applicable horizons. Enrichment device discovery SHALL be published alongside model devices once the timeout/collection completes. On HA birth ("online" on status topic), it SHALL re-publish all discovery config payloads using the current learned capability state. It SHALL be a no-op when `DiscoveryEnabled` is false.

#### Scenario: DiscoveryActor subscribes to EgressActor hub
- **WHEN** `DiscoveryActor` starts with MQTT enabled
- **THEN** it SHALL send `RequestEgressSource` to `EgressActor` and materialize a stream that filters for `EgressEvent.CapabilityLearned`

#### Scenario: Discovery deferred until capabilities learned
- **WHEN** MQTT is enabled and `MqttConnectionActor` connects and notifies `DiscoveryActor`
- **THEN** `DiscoveryActor` SHALL NOT publish discovery immediately; it SHALL wait for `EgressEvent.CapabilityLearned` events from the hub

#### Scenario: All capabilities received triggers discovery
- **WHEN** `EgressEvent.CapabilityLearned` events arrive for all configured (location, model) pairs
- **THEN** `DiscoveryActor` SHALL publish discovery config payloads for all devices, filtered by each model's capabilities

#### Scenario: Actor not registered when MQTT disabled
- **WHEN** `Mqtt.Enabled` is `false`
- **THEN** `DiscoveryActor` is not registered in the actor system

#### Scenario: Timeout triggers partial discovery
- **WHEN** the timeout expires and 6 of 8 configured models have reported capabilities
- **THEN** `DiscoveryActor` SHALL publish discovery for the 6 reported models and enrichment devices; the 2 unreported models SHALL be skipped

#### Scenario: Discovery re-published on HA birth
- **WHEN** HA publishes "online" on the status topic after capabilities have been learned
- **THEN** `DiscoveryActor` re-publishes all discovery config payloads using current learned state

#### Scenario: Late capability after timeout triggers incremental discovery
- **WHEN** a model reports capabilities after the initial discovery was already published
- **THEN** `DiscoveryActor` SHALL publish the discovery payload for that model immediately
