## MODIFIED Requirements

### Requirement: Mqtt message namespace

Messages related to MQTT connection lifecycle SHALL be defined in the `Njord.Messages.Mqtt` namespace within the Njord.Messages project. This SHALL include connection state notifications and inbound subscription management.

#### Scenario: MQTT connected notification is a shared message
- **WHEN** MqttConnection establishes a broker connection
- **THEN** it SHALL notify listeners using a message type defined in Njord.Messages.Mqtt

#### Scenario: Inbound subscription is a shared message
- **WHEN** an actor wants to receive inbound MQTT messages from MqttConnection
- **THEN** it SHALL send a subscription message defined in Njord.Messages.Mqtt

## REMOVED Requirements

### Requirement: Stream shutdown messages are actor-internal
**Reason**: `StopStreams`, `StreamsStopped`, `StopStreamsOf` and the coordinated-shutdown stream task no longer exist; stream lifecycle is bound to the owning actor.
**Migration**: See `stream-composition` ("Stream lifecycle is bound to the owning actor").
