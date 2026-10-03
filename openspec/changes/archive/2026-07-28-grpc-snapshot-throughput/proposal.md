## Why

gRPC `GetForecast` requests timeout (5s `AskTimeoutException`) when they arrive during a poll cycle. The `GrpcSnapshotConsumerActor` serializes ~49 `Ask` calls per cycle through `SelectAsync(1)`, blocking the `ForecastSnapshotActor` and `EnrichmentSnapshotActor` mailboxes for the duration. A read request queued behind 49 sequential write round-trips starves.

## What Changes

- **GrpcSnapshotConsumerActor stream graph** — replace `SelectAsync(1)` + `Ask<Ack>` with `Tell` (fire-and-forget). The consumer stream no longer waits for acknowledgement from snapshot actors, eliminating the sequential bottleneck.
- **Snapshot actor update handlers** — remove `Sender.Tell(new Ack(), Self)` from `UpdateForecast` and `UpdateEnrichment` handlers (no consumer waits for it).
- **Read-priority mailbox** — introduce `ReadPriorityMailbox` that gives read messages (`GetForecast`, `GetAllForecasts`, `GetEnrichment`, `GetAllEnrichments`) priority over write messages (`UpdateForecast`, `UpdateEnrichment`). Configured on `ForecastSnapshotActor` and `EnrichmentSnapshotActor`.
- **Remove `Ack` message type** if no other consumer uses it.

## Non-goals

- No changes to ForecastSnapshotActor persistence strategy (SaveSnapshot stays as-is)
- No changes to the gRPC service layer (Ask timeout remains 5s)
- No changes to EgressActor BroadcastHub topology
- No changes to ModelSnapshot/ForecastPoint memory layout (separate concern)
- No API budget impact — this change does not add or alter polling

## Capabilities

### New Capabilities
- `read-priority-mailbox`: Custom Akka mailbox that prioritizes read queries over write updates for snapshot actors

### Modified Capabilities
- `publisher-protocol`: The GrpcSnapshotConsumerActor stream graph changes from Ask-based to Tell-based for snapshot updates

## Impact

- **Files**: `src/Njord/Grpc/GrpcSnapshotConsumerActor.cs`, `src/Njord/Grpc/ForecastSnapshotActor.cs`, `src/Njord/Grpc/EnrichmentSnapshotActor.cs`, `src/Njord/Grpc/GrpcMessages.cs` (Ack removal), new `src/Njord/Grpc/ReadPriorityMailbox.cs`, `src/Njord/Configuration/NjordActorSystemSetup.cs` (mailbox config)
- **Tests**: `ForecastSnapshotActorSpec`, `EnrichmentSnapshotActorSpec`, `GrpcSnapshotConsumerActorSpec` — update to reflect Tell semantics and priority mailbox
- **No external API or protocol changes** — gRPC contract unchanged
