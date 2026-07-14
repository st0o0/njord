# mqtt-egress Delta Specification

## REMOVED Requirements

### Requirement: Actor-owned broker connection with LWT availability
**Reason**: Connection lifecycle moves to `MqttConnectionActor` in `Njord.Mqtt`.
**Migration**: `MqttConnectionActor` owns `IMqttConnection`, `IMqttTransport`, reconnect logic, and LWT publishing.

### Requirement: SinkRef vending via MergeHub
**Reason**: MergeHub ownership moves to `MqttConnectionActor`.
**Migration**: `MqttConnectionActor` vends `SinkRef<MqttMessage>` via `RequestMqttSink`/`MqttSinkResponse`.

### Requirement: Discovery config payloads for enrichment pseudo-devices
**Reason**: Discovery publishing moves to `DiscoveryActor` in `Njord.Mqtt`.
**Migration**: `DiscoveryActor` handles HA birth subscription and publishes all device configs using `DiscoveryPayloadBuilder`.

### Requirement: Per-horizon retained state topics with flat JSON
**Reason**: State publishing moves to `MqttPublisherActor` in `Njord.Mqtt`.
**Migration**: `MqttPublisherActor` receives domain results from `EgressActor`, transforms via `StatePayloadBuilder`, and publishes via MergeHub sink.
