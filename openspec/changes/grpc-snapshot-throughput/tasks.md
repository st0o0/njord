## 1. ReadPriorityMailbox

- [x] 1.1 Create `src/Njord/Grpc/ReadPriorityMailbox.cs` — subclass `UnboundedStablePriorityMailbox`, assign priority 0 to `GetForecast`/`GetAllForecasts`/`GetEnrichment`/`GetAllEnrichments`, priority 1 to `UpdateForecast`/`UpdateEnrichment`, priority 2 to all other messages
- [x] 1.2 Configure `ForecastSnapshotActor` and `EnrichmentSnapshotActor` to use the priority mailbox in `src/Njord/Configuration/NjordActorSystemSetup.cs` via `.WithActors` props

## 2. Tell-based snapshot updates

- [x] 2.1 Update `src/Njord/Grpc/GrpcSnapshotConsumerActor.cs` — replace `SelectAsync(1, async e => await actor.Ask<Ack>(...))` with `Select(e => { actor.Tell(...); return e; })` in `MaterializeGraph`
- [x] 2.2 Remove `Sender.Tell(new Ack(), Self)` from `UpdateForecast` handler in `src/Njord/Grpc/ForecastSnapshotActor.cs`
- [x] 2.3 Remove `Sender.Tell(new Ack(), Self)` from `UpdateEnrichment` handler in `src/Njord/Grpc/EnrichmentSnapshotActor.cs`
- [x] 2.4 Remove `Ack` record from `src/Njord/Grpc/SnapshotMessages.cs`

## 3. Test updates

- [x] 3.1 Update `src/Njord.Tests/Grpc/ForecastSnapshotActorSpec.cs` — replace `Ask<Ack>(UpdateForecast)` with `Tell(UpdateForecast)` + short delay or `ExpectNoMsg` to confirm processing
- [x] 3.2 Update `src/Njord.Tests/Grpc/EnrichmentSnapshotActorSpec.cs` — same pattern: Tell + verify via subsequent Ask for read
- [x] 3.3 Add `src/Njord.Tests/Grpc/ReadPriorityMailboxSpec.cs` — verify read messages are dequeued before write messages when both are pending

## 4. Validation

- [x] 4.1 Run `dotnet build Njord.slnx` from `src/` to verify compilation
- [ ] 4.2 Run `dotnet run --project Njord.Tests/Njord.Tests.csproj` to verify all tests pass
