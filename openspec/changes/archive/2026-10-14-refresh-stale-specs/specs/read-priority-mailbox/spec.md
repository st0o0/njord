## REMOVED Requirements

### Requirement: GrpcSnapshotConsumerActor uses Tell for snapshot updates
**Reason**: The snapshot actors this mailbox served (`ForecastSnapshotActor`, `EnrichmentSnapshotActor`) and `GrpcSnapshotConsumerActor` no longer exist, and no actor uses a custom priority mailbox. The `Ack` record it claimed removed still exists (src/Njord.Messages/Common/SharedMessages.cs) and is sent by `PollScheduler`.
**Migration**: Reads are served from `ForecastReadModel`'s in-memory state; see `consumer-model`.

### Requirement: Snapshot actors do not reply to updates
**Reason**: The snapshot actors this mailbox served (`ForecastSnapshotActor`, `EnrichmentSnapshotActor`) and `GrpcSnapshotConsumerActor` no longer exist, and no actor uses a custom priority mailbox. The `Ack` record it claimed removed still exists (src/Njord.Messages/Common/SharedMessages.cs) and is sent by `PollScheduler`.
**Migration**: Reads are served from `ForecastReadModel`'s in-memory state; see `consumer-model`.

### Requirement: Read-priority mailbox for snapshot actors
**Reason**: The snapshot actors this mailbox served (`ForecastSnapshotActor`, `EnrichmentSnapshotActor`) and `GrpcSnapshotConsumerActor` no longer exist, and no actor uses a custom priority mailbox. The `Ack` record it claimed removed still exists (src/Njord.Messages/Common/SharedMessages.cs) and is sent by `PollScheduler`.
**Migration**: Reads are served from `ForecastReadModel`'s in-memory state; see `consumer-model`.

### Requirement: Ack message removed
**Reason**: The snapshot actors this mailbox served (`ForecastSnapshotActor`, `EnrichmentSnapshotActor`) and `GrpcSnapshotConsumerActor` no longer exist, and no actor uses a custom priority mailbox. The `Ack` record it claimed removed still exists (src/Njord.Messages/Common/SharedMessages.cs) and is sent by `PollScheduler`.
**Migration**: Reads are served from `ForecastReadModel`'s in-memory state; see `consumer-model`.
