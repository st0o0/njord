## REMOVED Requirements

### Requirement: EgressActor accepts publisher registration
**Reason**: Replaced by streams-based MergeHub/BroadcastHub in the new EgressActor. Producers attach via `ISinkRef<EgressEvent>`, consumers via `ISourceRef<EgressEvent>`.
**Migration**: Actors that sent `RegisterPublisher` now send `RequestEgressSink` or `RequestEgressSource` to the EgressActor and attach via StreamRefs.

### Requirement: EgressActor broadcasts results to registered publishers
**Reason**: Tell-based fan-out replaced by BroadcastHub which provides backpressure-aware streaming distribution.
**Migration**: Consumers subscribe to the EgressActor's BroadcastHub via `RequestEgressSource` and process `EgressEvent` in their own stream graph.

### Requirement: EgressActor accepts publisher unregistration
**Reason**: StreamRef lifecycle replaces manual unregistration. Consumer disconnection is handled by stream completion/failure.
**Migration**: No explicit unregistration needed — stream completion handles cleanup.
