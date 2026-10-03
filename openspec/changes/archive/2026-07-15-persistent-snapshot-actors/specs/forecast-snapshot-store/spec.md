## REMOVED Requirements

### Requirement: ForecastSnapshotStore captures latest forecast per model
**Reason**: Replaced by `ForecastSnapshotActor` (Akka Persistence). The `ConcurrentDictionary`-based store and its `SnapshotConsumerActor` are removed in favor of a proper actor with persistence, Ask/Ack messaging, and supervision.
**Migration**: Use `ForecastSnapshotActor` via Ask pattern instead of injecting `ForecastSnapshotStore`.

### Requirement: SnapshotConsumerActor subscribes to egress BroadcastHub
**Reason**: Replaced by the new `SnapshotConsumerActor` in `Njord.Grpc` which routes to both `ForecastSnapshotActor` and `EnrichmentSnapshotActor` via Ask/Ack.
**Migration**: The new `SnapshotConsumerActor` handles both forecast and enrichment events in one actor.
