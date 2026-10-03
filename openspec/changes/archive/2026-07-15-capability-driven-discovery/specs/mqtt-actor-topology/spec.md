## MODIFIED Requirements

### Requirement: DiscoveryActor publishes HA discovery configs
The `DiscoveryActor` SHALL request a `SinkRef<MqttMessage>` from `MqttConnectionActor`. It SHALL subscribe to the HA status topic (`{discoveryPrefix}/status`) via `MqttConnectionActor`. It SHALL NOT publish discovery on initial connection. Instead, it SHALL collect `ModelCapabilityLearned` messages from `ModelStateActor` instances. Once all expected (location, model) pairs have reported — or a configurable timeout expires (default: 2× poll interval) — it SHALL publish retained discovery config payloads for all reported devices using `DiscoveryPayloadBuilder`, filtered by each model's supported parameters and applicable horizons. Enrichment device discovery (consensus, alerts, derived, trends, indices, energy, history) SHALL be published alongside model devices once the timeout/collection completes, using their existing `IEnrichmentFeature.BuildDiscoveryPayload` methods unmodified. On HA birth ("online" on status topic), it SHALL re-publish all discovery config payloads using the current learned capability state. It SHALL be a no-op when `DiscoveryEnabled` is false.

#### Scenario: Discovery deferred until capabilities learned
- **WHEN** `MqttConnectionActor` connects and notifies `DiscoveryActor`
- **THEN** `DiscoveryActor` SHALL NOT publish discovery immediately; it SHALL wait for `ModelCapabilityLearned` messages

#### Scenario: All capabilities received triggers discovery
- **WHEN** `ModelCapabilityLearned` messages arrive for all configured (location, model) pairs
- **THEN** `DiscoveryActor` SHALL publish discovery config payloads for all devices, filtered by each model's capabilities

#### Scenario: Timeout triggers partial discovery
- **WHEN** the timeout expires and 6 of 8 configured models have reported capabilities
- **THEN** `DiscoveryActor` SHALL publish discovery for the 6 reported models and enrichment devices; the 2 unreported models SHALL be skipped

#### Scenario: Late capability after timeout triggers incremental discovery
- **WHEN** a model reports capabilities after the initial discovery was already published
- **THEN** `DiscoveryActor` SHALL publish the discovery payload for that model immediately

#### Scenario: Discovery re-published on HA birth
- **WHEN** HA publishes "online" on the status topic after capabilities have been learned
- **THEN** `DiscoveryActor` re-publishes all discovery config payloads using current learned state

#### Scenario: Discovery disabled
- **WHEN** `DiscoveryEnabled` is false
- **THEN** `DiscoveryActor` does not publish configs, does not subscribe to HA status, and ignores `ModelCapabilityLearned` messages

#### Scenario: Capability expansion triggers re-discovery for affected device
- **WHEN** `DiscoveryActor` receives an updated `ModelCapabilityLearned` with additional parameters for a model that was already published
- **THEN** it SHALL re-publish the discovery payload for that device with the expanded component set
