## REMOVED Requirements

### Requirement: HA Discovery is configurable via DiscoveryEnabled
**Reason**: MQTT Discovery was removed entirely (archived change `2026-10-10-restructure-enrichment-pipeline`). `MqttOptions` has no `DiscoveryEnabled` property (src/Njord.Core/Configuration/Mqtt/MqttOptions.cs) and no discovery config payload is published.
**Migration**: None. MQTT publishes state topics and availability only; see `mqtt-egress`.
