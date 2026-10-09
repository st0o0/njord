## 1. ForecastHistoryState.ProcessSnapshot

- [x] 1.1 Add `ForecastHistoryState.ProcessSnapshot(ForecastSnapshot snapshot, string location, ResolvedParameterSet parameters, DateTimeOffset now) -> (ForecastHistoryState, ForecastRecord)` to `src/Njord.Enrichment/ForecastHistoryState.cs`, moving the nearest-point lookup, per-parameter value extraction, and `ConsensusComputer.ComputeMedian` call out of `ForecastHistoryActor.OnRecordSnapshot`.
- [x] 1.2 Update `ForecastHistoryActor.OnRecordSnapshot` (`src/Njord.Enrichment/ForecastHistoryActor.cs`) to call `_state.ProcessSnapshot(...)`, then `Persist` the returned event; keep snapshot-interval/`SaveSnapshot` logic in the `Persist` callback unchanged.
- [x] 1.3 Add `ForecastHistoryStateSpec` cases in `src/Njord.Enrichment.Tests/` covering: nearest-point selection when multiple models report, consensus median with partial model coverage, and the case where no snapshot entries match the actor's location.
- [x] 1.4 Confirm existing `ForecastHistoryActorSpec` behavior assertions (persisted event shape, snapshot cadence) still pass unmodified — no event/DTO shape change expected.

## 2. SchedulerState / ModelPollState decision methods

- [x] 2.1 Add `ModelPollState`/`SchedulerState.ApplyHashResult(string key, string hash, DateTimeOffset now, TimeSpan discoveryInterval) -> (SchedulerState, HashDecision)` to `src/Njord.Pipeline/SchedulerState.cs` / `src/Njord.Pipeline/ModelPollState.cs`, where `HashDecision` is `Changed(DataChanged evt)` or `Miss`.
- [x] 2.2 Add `SchedulerState.ApplyRateLimited(string key, DateTimeOffset now, TimeSpan minDelay, TimeSpan discoveryInterval) -> SchedulerState`, folding the existing `ApplyTransientFailure` call and the `NextPollUtc` floor adjustment into one step.
- [x] 2.3 Update `SchedulerActor.OnHashResult` (`src/Njord.Pipeline/SchedulerActor.cs`) to call `_state.ApplyHashResult(...)` and branch on the returned `HashDecision`, keeping `Persist`, logging, metrics, `ScheduleNext`, and `Sender.Tell(new Ack())` where they are.
- [x] 2.4 Update `SchedulerActor.OnFetchFailed`'s `RateLimited` case to call `_state.ApplyRateLimited(...)` instead of reading/mutating/reassigning `ModelPollState` inline.
- [x] 2.5 Add `SchedulerStateSpec` cases in `src/Njord.Pipeline.Tests/` covering: hash changed vs. unchanged decisions, and rate-limit floor applied only when the computed `NextPollUtc` would otherwise be sooner than `now + RateLimitMinDelay`.
- [x] 2.6 Confirm existing `SchedulerActorSpec`/`SchedulerActorRefFailureSpec` tests still pass unmodified.

## 3. Location lookup helper

- [x] 3.1 Add `NjordOptionsExtensions.FindLocation(this NjordOptions options, string name) -> LocationOptions?` in `src/Njord.Core/Configuration/`.
- [x] 3.2 Replace the three independent `_options.Locations.FirstOrDefault(l => l.Name.Equals(...))` call sites in `SchedulerActor.CreateTarget`, `OnTriggerImmediatePoll`, and `RecordPollCompletion` with calls to the new helper.
- [x] 3.3 Add a `NjordOptionsExtensionsSpec` (or extend an existing `Njord.Core.Tests` configuration spec) covering case-insensitive match and no-match cases.

## 4. Delete-failure handlers

- [x] 4.1 Add `Command<DeleteMessagesFailure>` and `Command<DeleteSnapshotsFailure>` handlers to `BudgetTrackerActor` (`src/Njord.Pipeline/BudgetTrackerActor.cs`), logging a warning with the failure cause, matching the existing `SaveSnapshotFailure` handler's log shape.
- [x] 4.2 Add the same two handlers to `ForecastHistoryActor` (`src/Njord.Enrichment/ForecastHistoryActor.cs`).
- [x] 4.3 Add test cases in `BudgetTrackerActorSpec` (`src/Njord.Pipeline.Tests/`) and `ForecastHistoryActorSpec` (`src/Njord.Enrichment.Tests/`) asserting a warning is logged (via `TestProbe` subscribed to the actor's `Warning` log event or `Logging.Warning` capture per existing test conventions) when `DeleteMessagesFailure`/`DeleteSnapshotsFailure` is sent.

## 5. MqttConnectionActor backoff and failure handling

- [ ] 5.1 Replace the hand-rolled `Math.Pow(2, Math.Min(_connectAttempts - 1, 6))` backoff in `src/Njord.Mqtt/MqttConnectionActor.cs` with the injected `Backoff.Create()` policy already used by `SchedulerActor`, configured to produce an equivalent (or documented, intentionally adjusted) delay sequence.
- [ ] 5.2 Add a failure path for the post-connect `SubscribeAsync` call: surface it as a logged warning plus a typed failure message (reuse an existing failure type or add one), instead of only logging and continuing.
- [ ] 5.3 Add `MqttConnectionActorSpec` cases asserting the new backoff delay sequence and the new subscription-failure behavior.

## 6. NSubstitute cleanup (Njord.Pipeline.Tests)

- [ ] 6.1 Add the NSubstitute package reference: `dotnet add src/Njord.Pipeline.Tests/Njord.Pipeline.Tests.csproj package NSubstitute` (run from `src/`; this is the solution's first use of NSubstitute, version lands in `Directory.Packages.props` via central package management).
- [ ] 6.2 Replace `FakeProvider : IBudgetProvider` in `BudgetThrottleStageSpec.cs` with `Substitute.For<IBudgetProvider>()` + `.Rate.Returns(...)`.
- [ ] 6.3 Replace `RecordingLogger : ILogger` in `StreamSupervisionSpec.cs` with `Substitute.For<ILogger>()`, asserting via `sub.Received(...)` instead of a custom recorded-entries list.
- [ ] 6.4 Replace the four `AlwaysAllow*Gate` stubs (`PipelineConnectionSpec.cs` x2, `PipelineActorShutdownSpec.cs`, `SinkRefConnectionSpec.cs`) with `Substitute.For<IBudgetGate<T>>()` returning `true`/zero delay.
- [ ] 6.5 Confirm the six stateful/recording fakes (`FakeConnection`, `RecordingTransport`, the local `FakeOpenMeteoClient`/`TimingOpenMeteoClient`, `AlwaysAllowGate`/`RejectThenAllowGate` that record acquired elements) are left unchanged.

## 7. Architecture test: Akka-free foundation layers

- [ ] 7.1 Add a `[Fact]` to `src/Njord.Architecture.Tests/LayerReferenceSpec.cs` (or `ZoneArchitectureSpec.cs`) asserting `Njord.Weather`, `Njord.Sensors`, `Njord.Messages`, and `Njord.Persistence` have no dependency on any `Akka.*` namespace.
- [ ] 7.2 Run the architecture test suite and confirm it passes against current code (no violations expected — this is a regression guard, not a fix).

## Validation

- [x] 8.1 `dotnet build Njord.slnx` (run from `src/`)
- [x] 8.2 `dotnet run --project Njord.Enrichment.Tests/Njord.Enrichment.Tests.csproj --no-build`
- [x] 8.3 `dotnet run --project Njord.Pipeline.Tests/Njord.Pipeline.Tests.csproj --no-build`
- [x] 8.4 `dotnet run --project Njord.Core.Tests/Njord.Core.Tests.csproj --no-build`
- [ ] 8.5 `dotnet run --project Njord.Mqtt.Tests/Njord.Mqtt.Tests.csproj --no-build`
- [ ] 8.6 `dotnet run --project Njord.Architecture.Tests/Njord.Architecture.Tests.csproj --no-build`
- [ ] 8.7 `dotnet slopwatch analyze -d . --fail-on warning` (run from repo root)
