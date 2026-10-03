## REMOVED Requirements

### Requirement: ModelStateActor skips discovery notification when MQTT is disabled
**Reason**: `ModelStateActor` no longer references `DiscoveryActor` or `Njord.Mqtt`. Capability data flows through the `EgressActor` hub as `EgressEvent.CapabilityLearned`. When MQTT is disabled and `DiscoveryActor` is not registered, no consumer subscribes for capability events — they are simply dropped by the BroadcastHub. No conditional logic needed in `ModelStateActor`.
**Migration**: Remove `_mqttEnabled`, `_discoveryActor`, and the null-guard logic from `ModelStateActor`. The `optional-mqtt-egress` spec's remaining requirement ("MQTT egress is disabled via config flag") is unchanged.
