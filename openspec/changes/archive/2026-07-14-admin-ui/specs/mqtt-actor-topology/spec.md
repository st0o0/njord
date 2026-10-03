# mqtt-actor-topology Delta Specification (admin-ui)

## MODIFIED Requirements

### Requirement: DiscoveryActor publishes HA discovery configs
The `DiscoveryActor` SHALL request a `SinkRef<MqttMessage>` from `MqttConnectionActor`. It SHALL subscribe to the HA status topic (`{discoveryPrefix}/status`) via `MqttConnectionActor`. On connection and on HA birth ("online" on status topic), it SHALL publish retained discovery config payloads for all configured devices (per-model devices, consensus, alerts, derived, trends, indices, energy, history) using `DiscoveryPayloadBuilder`. It SHALL be a no-op when `DiscoveryEnabled` is false.

The `DiscoveryActor` SHALL use `IOptionsMonitor<NjordOptions>` (not `IOptions<NjordOptions>`) and read `.CurrentValue` when computing the device set. It SHALL maintain a set of previously published device IDs. On receiving a `RefreshDiscovery` message, it SHALL diff the current device set against the published set, publish discovery payloads for new devices, and publish empty retained messages (tombstones) for removed devices.

#### Scenario: Discovery published on connect
- **WHEN** `MqttConnectionActor` connects and notifies `DiscoveryActor`
- **THEN** `DiscoveryActor` publishes discovery config payloads for all devices

#### Scenario: Discovery re-published on HA birth
- **WHEN** HA publishes "online" on the status topic
- **THEN** `DiscoveryActor` re-publishes all discovery config payloads

#### Scenario: Discovery disabled
- **WHEN** `DiscoveryEnabled` is false
- **THEN** `DiscoveryActor` does not publish configs and does not subscribe to HA status

#### Scenario: RefreshDiscovery adds new devices
- **WHEN** `RefreshDiscovery` is received after a new model was added
- **THEN** discovery payloads for the new model's device are published

#### Scenario: RefreshDiscovery tombstones removed devices
- **WHEN** `RefreshDiscovery` is received after a model was removed
- **THEN** an empty retained message is published to the removed device's discovery topic

#### Scenario: RefreshDiscovery with no changes is a no-op
- **WHEN** `RefreshDiscovery` is received but the device set has not changed
- **THEN** no MQTT messages are published
