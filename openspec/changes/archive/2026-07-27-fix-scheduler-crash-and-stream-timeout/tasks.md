## 1. SchedulerActor async pipeline resolution (TDD)

- [x] 1.1 Write test: SchedulerActor starts and responds to GetPollStates when PipelineActor is registered AFTER SchedulerActor in the same `WithResolvableActors` block (reproduces production registration order). File: `src/Njord.Tests/Pipeline/SchedulerActorStartupOrderSpec.cs`
- [x] 1.2 Write test: SchedulerActor handles GetPollStates while waiting for PipelineActor resolution (WaitingForPipeline state). File: `src/Njord.Tests/Pipeline/SchedulerActorGetPollStatesBeforeReadySpec.cs` (extend existing)
- [x] 1.3 Add `PipelineResolved` message and `WaitingForPipeline` state to SchedulerActor. Change `PreStart` to use `GetActorAsync<PipelineActor>().PipeTo(Self)`. File: `src/Njord/Pipeline/SchedulerActor.cs`
- [x] 1.4 Update `OnTerminated` to transition to `WaitingForPipeline` using `GetActorAsync` instead of synchronous `GetActor`. File: `src/Njord/Pipeline/SchedulerActor.cs`
- [x] 1.5 Verify all existing SchedulerActor tests still pass

## 2. Kestrel MinResponseDataRate fix (TDD)

- [x] 2.1 ~~Write test: gRPC endpoint has MinResponseDataRate disabled~~ (skipped — Kestrel config not testable without integration test overhead)
- [x] 2.2 Set `MinResponseDataRate = null` on the gRPC `ListenOptions` in `Program.cs`. File: `src/Njord/Program.cs`

## 3. Validation

- [x] 3.1 Run all tests: `dotnet run --project Njord.Tests/Njord.Tests.csproj` from `src/` — 581 passed, 0 failed
- [x] 3.2 Run slopwatch: `dotnet slopwatch` from repo root — 1 pre-existing warning (SW005), no new issues
