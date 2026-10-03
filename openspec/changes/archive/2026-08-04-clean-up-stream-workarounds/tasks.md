# Tasks: Clean Up Stream Workarounds

## A — Refactor GrpcSnapshotConsumerActor to extend StreamConsumerActor
- [x] Make `GrpcSnapshotConsumerActor` extend `StreamConsumerActor` instead of `ReceiveActor`
- [x] Remove duplicated fields: `_watchedDeps`, `_lastTerminatedRef`, `_retryCount`, `_killSwitch`, `Stash`
- [x] Remove duplicated methods: `OnTerminated`, `ScheduleRetryResolve`, `WaitingForSource` state machine
- [x] Implement `ResolveDependencies`: resolve EgressActor via `GetActorAsync`
- [x] Implement `ConfigureWaitingForRefs`: handle `EgressResolved`, `EgressSourceResponse`, `SnapshotActorsResolved`
- [x] Implement `AllRefsReady`: check all three refs are set
- [x] Implement `MaterializeGraph(killSwitch)`: existing graph with kill-switch param
- [x] Replace `is var _ ? update : update` with normal async body in `SelectAsync`
- [x] Remove empty `PostStop()` override
- [x] Run existing `GrpcSnapshotConsumerActorSpec` tests

## B — Replace TakeWhile with KillSwitch in gRPC streaming
- [x] `StreamForecasts`: create `SharedKillSwitch`, register CT, replace `TakeWhile` with `.Via(ks.Flow<T>())`
- [x] `StreamEnrichments`: same pattern
- [x] Keep `context.CancellationToken` on `WriteAsync` calls
- [x] Run existing gRPC tests

## C — Narrow bare catch in EnrichmentActor
- [x] Replace `catch { ... }` with `catch (AskTimeoutException)` in `BuildConsensusInlineFlow`
- [x] `AskTimeoutException` already in `Akka.Actor` (already imported)
- [x] Run `EnrichmentActorSpec` tests

## D — Convert mutable closures to Scan
- [x] EnrichmentActor: `previous` → Scan with `(Previous, Current)` pair + Skip(1)
- [x] ModelStateActor: kept as-is — dictionary mutation inside Scan would be equally mutable; state is scoped to materialization, parallelism=1
- [x] MqttEgressActor: kept as-is — same reasoning as ModelStateActor
- [x] Run tests for all three actors

## E — Remove empty PostStop overrides
- [x] PipelineActor: remove empty `PostStop()`
- [x] SchedulerActor: remove empty `PostStop()`
- [x] (GrpcSnapshotConsumerActor already handled in task A)

## F — Verification
- [x] `dotnet build Njord.slnx` — 0 errors
- [x] `dotnet run --project Njord.Tests/Njord.Tests.csproj` — 648 passed, 0 failed
- [x] `dotnet slopwatch` — 1 pre-existing warning (SW005 xUnit1051 NoWarn), no new issues
- [x] `dotnet format --verify-no-changes` — clean
