## Why

10 actor/stream test specs manage their own `ActorSystem` via raw
`ActorSystem.Create` + `IDisposable`/`IAsyncDisposable`. This means manual
lifecycle management, no access to `CreateTestProbe()` or TestKit assertions
(`ExpectMsg`, `ExpectNoMsg`), and a 10-second coordinated-shutdown penalty on
every test that requires HOCON workarounds. `Akka.TestKit.Xunit.TestKit` and
`Akka.Persistence.TestKit.PersistenceTestKit` already handle all of this —
lifecycle, `Sys`, probes — and are available transitively via the existing
`Akka.Persistence.TestKit.Xunit` package.

## What Changes

- Migrate 8 specs from raw `ActorSystem` + `IDisposable`/`IAsyncDisposable` to
  `Akka.TestKit.Xunit.TestKit` base class.
- Migrate 2 specs (snapshot actor specs that use persistent actors internally)
  from raw `ActorSystem` to `PersistenceTestKit`.
- Replace `_system` field references with `Sys` throughout.
- Remove manual `Dispose`/`DisposeAsync`, HOCON shutdown-timeout overrides, and
  `ActorSystem.Create` calls.
- Add `Akka.TestKit.Xunit` as an explicit package reference (currently only
  transitively available).

## Non-goals

- Rewriting test logic or assertions (the `MessageCollector` pattern from the
  previous change stays where it's already in use).
- Migrating the 7 specs that already use `PersistenceTestKit` — those are fine.
- Changing production code.

## API-budget impact

No impact — test-only change.

## Capabilities

### New Capabilities

_(none)_

### Modified Capabilities

- `test-project-structure`: Updating the actor-test requirement to specify
  TestKit/PersistenceTestKit base classes instead of raw `ActorSystem.Create`.

## Impact

- `src/Njord.Tests/Egress/ModelStateActorSpec.cs` → `TestKit`
- `src/Njord.Tests/Egress/EgressActorSpec.cs` → `TestKit`
- `src/Njord.Tests/Mqtt/MqttConnectionActorSpec.cs` → `TestKit`
- `src/Njord.Tests/Mqtt/DiscoveryActorSpec.cs` → `TestKit`
- `src/Njord.Tests/Pipeline/PollPipelineSpec.cs` → `TestKit`
- `src/Njord.Tests/Grpc/ForecastGrpcServiceSpec.cs` → `TestKit`
- `src/Njord.Tests/Grpc/ConfigGrpcServiceSpec.cs` → `TestKit`
- `src/Njord.Tests/Enrichment/EnrichmentActorSpec.cs` → `TestKit`
- `src/Njord.Tests/Grpc/ForecastSnapshotActorSpec.cs` → `PersistenceTestKit`
- `src/Njord.Tests/Grpc/EnrichmentSnapshotActorSpec.cs` → `PersistenceTestKit`
- `src/Njord.Tests/Njord.Tests.csproj` — add explicit `Akka.TestKit.Xunit` reference
- `src/Directory.Packages.props` — add version entry for `Akka.TestKit.Xunit`
