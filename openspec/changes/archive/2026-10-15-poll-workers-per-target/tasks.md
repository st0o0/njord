## 1. Contracts and routing

- [x] 1.1 Specs first: `src/Njord.Core.Tests/Actors/ShardMessageExtractorSpec.cs` (entity id of every `IWithTargetId` message is its target id; same id -> same shard; an unknown message throws naming its type) and `src/Njord.Messages`-level checks in the same spec for `PollTarget.Id`/`ToString()`. Run and see them fail.
- [x] 1.2 Add `IWithTargetId`, `PollTarget`, `StartPolling`, `StopPolling`, `TriggerTargetPoll`, `PollWorkerReport` + `PollWorkerOutcome` in `src/Njord.Messages/Pipeline/` (one file each); add `IPollManager` and `IPollRegion` to `src/Njord.Core/Actors/ActorKeys.cs`; add the `Akka.Cluster.Hosting` package to `Njord.Core` (`dotnet add package`) and `src/Njord.Core/Actors/ShardMessageExtractor.cs`.

## 2. Poll worker state and persistence records

- [x] 2.1 Specs first: `src/Njord.Pipeline.Tests/PollWorkerStateSpec.cs` porting every scenario of `ModelPollStateSpec` and `SchedulerStateSpec` onto `Apply(event)` (discovery, steady, misses, fallback, transient counter and cap, rate-limit floor, deferral, start/replan/stop, snapshot and persistence roundtrip, initial state) and `src/Njord.Persistence.Tests` roundtrip + Verify wire-format specs for `PollingStarted`, `PollingStopped`, `ForecastChanged`, `ForecastUnchanged`, `PollFailed`, `PollDeferred`, `PersistedPollWorkerState`. Run and see them fail.
- [x] 2.2 Add the records in `src/Njord.Persistence/Events/Polling/` and `src/Njord.Pipeline/PollWorkerState.cs` (record + `PollWorkerStateExtensions`, `FromPersistence`, `GetPersistenceState`, `GetSnapshot`).

## 3. Poll worker actor

- [x] 3.1 Specs first in `src/Njord.Pipeline.Tests/PollWorkerSpec.cs` and `PollWorkerFetchReplySpec.cs` (worker created directly from the resolver, probe as `IPollManager`, fake gateways): polls on start and reports Planned; changed/unchanged/failed reports with the new state; reply spelled differently updates the configured target; every failure reason and rejection (Budget >= 5 min, QueueFull/Expired transient, PipelineUnavailable 5 s without counting); Ask timeout -> Transport backoff (never silent); missing gateway -> Transport; at most one request in flight (trigger during a request sends nothing); trigger reaches the gateway; replaced gateway used by the next poll; terminated gateway causes no poll loop; discovery replanning via a second `StartPolling` (shorter, longer, unchanged); `StopPolling` persists, stops and keeps the journal; recovery: restart the worker -> same phase/cycle/next poll, past next poll polls immediately, snapshot after 50 events with recovery from snapshot. Run and see them fail.
- [x] 3.2 Implement `src/Njord.Pipeline/PollWorker.cs` (`ReceivePersistentActor`, `IWithTimers`, request id, `TryGetActor<IForecastGateway>`, `PipeTo`, persist + `Apply`, snapshot interval 50 with journal cleanup and failure logging, reports to `IPollManager`).

## 4. Poll manager actor

- [x] 4.1 Specs first in `src/Njord.Pipeline.Tests/PollManagerSpec.cs` and `PollManagerCycleSpec.cs` (probe as `IPollRegion`): `StartPolling` for every resolved target on start (union of global and location models); late region registration; query answered before the first report and from reports (all configured, discovery initial state, state after change, miss count); report for unknown target ignored; `TriggerImmediatePoll` (one pair, case-insensitive, all, unknown location, region missing -> failed); `PollingChanged` re-sends `StartPolling` only when the interval changes; region state with an unconfigured entity -> `StopPolling`; terminated worker restarted; health snapshot updated on changed/unchanged; cycle metrics (one failed model, rejection counts as failed, consecutive rounds) and data-changed counter. Run and see them fail.
- [x] 4.2 Implement `src/Njord.Pipeline/PollManager.cs` reusing `PollCycleTracker` and `Telemetry`.

## 5. Switch over (refactor!)

- [x] 5.1 Spec first: `src/Njord.Pipeline.Tests/PollingClusterSpec.cs` - hosted single-node cluster (remoting port 0, self join), in-memory persistence, `WithPolling()` registration, fake gateway: every configured target is fetched and its last change appears in `QueryPollStates`; a trigger reaches the gateway through the region. Run and see it fail.
- [x] 5.2 `src/Njord.Pipeline/Configuration/PipelineSetupContainer.cs`: internal `WithPolling()` (shard region `poll-worker`, `ShardOptions` without passivation and remember-entities, singleton `poll-manager` with `BackoffOnFailure`), used by the registration instead of the scheduler singleton.
- [x] 5.3 Rename `IPollScheduler` -> `IPollManager` in `src/Njord.Grpc/OpsGrpcService.cs` (log texts), `src/Njord/Configuration/ConfigManager.cs`, `src/Njord.Grpc.Tests/OpsGrpcServiceSpec.cs`, `src/Njord.Tests/Configuration/ConfigManagerSpec.cs`, `src/Njord.Pipeline.Tests/PipelineConnectionSpec.cs`; remove `IPollScheduler`.
- [x] 5.4 Remove `PollScheduler`, `PollSchedulerState`, `ModelPollState`, `ScheduledPoll`, `DataChanged`, `Njord.Persistence/Events/Pipeline/*` and their specs (`PollScheduler*Spec`, `SchedulerStateSpec`, `ModelPollStateSpec`, `DataChangedRoundtripSpec`, `PollSchedulerDtoRoundtripSpec`, the `DataChanged` wire-format fact, `SchedulerDtoSerializationSpec...verified.txt`); point `LayerReferenceSpec` at another Messages type; keep unrelated `*.verified.*` files untouched.
- [x] 5.5 Update `AGENTS.md` (guardrails, solution structure, test totals), `src/Njord/appsettings.Example.json` comments and `docs/` pages that describe the scheduler.

## 6. Validation

- [x] 6.1 From `src/`: `dotnet build Njord.slnx` with 0 warnings, 0 errors.
- [x] 6.2 `dotnet run --project Njord.Pipeline.Tests/Njord.Pipeline.Tests.csproj` three times, plus `dotnet run --project Njord.Core.Tests/Njord.Core.Tests.csproj`, `dotnet run --project Njord.Persistence.Tests/Njord.Persistence.Tests.csproj`, `dotnet run --project Njord.Grpc.Tests/Njord.Grpc.Tests.csproj`, `dotnet run --project Njord.Tests/Njord.Tests.csproj`, `dotnet run --project Njord.Architecture.Tests/Njord.Architecture.Tests.csproj`.
- [x] 6.3 The full sequential loop from `AGENTS.md`; update "Current total".
- [x] 6.4 Manual check twice (`cd src/Njord && timeout -s INT 90 dotnet run`, data backed up and restored byte-identically, second run on the first run's DB): shard region starts, targets polled and analyzed, poll summaries logged, no njord dead letters or unhandled messages; second run recovers the workers.
- [x] 6.5 `openspec validate --all --no-interactive`.
