## 1. Infrastructure — Akka.Persistence + SQLite

- [x] 1.1 Add `Akka.Persistence.Sqlite` package via `dotnet add package` to `src/Njord/Njord.csproj` and pin in `src/Directory.Packages.props`
- [x] 1.2 Configure Akka.Persistence SQLite journal and snapshot store in `src/Njord/Program.cs` via Akka.Hosting (connection string pointing to a data volume path, e.g. `data/njord-journal.db`)
- [x] 1.3 Add `data/` directory to `.dockerignore` allowlist and update `Dockerfile` to create the volume mount point

## 2. Domain — Hash Computation

- [x] 2.1 Create `src/Njord/Domain/ForecastDataHash.cs` — static `Compute(ModelForecast, TimeProvider)` method that hashes only `Values` dictionaries from hourly and daily points with `ValidAt >= tomorrow 00:00 UTC` cutoff, using `HashCode`
- [x] 2.2 Create `src/Njord.Tests/Domain/ForecastDataHashSpec.cs` — tests: same data same hash, different values different hash, timestamp-only change same hash, cutoff excludes today's points, null values are hashed consistently

## 3. Scheduler — Messages and State

- [x] 3.1 Create `src/Njord/Pipeline/SchedulerMessages.cs` — `HashResult(string Location, string ModelId, int Hash)`, `Ack`, `RequestPipelineQueue`, `PipelineQueueResponse`, `ScheduledPoll(string Location, string ModelId)` records
- [x] 3.2 Create `src/Njord/Pipeline/ModelPollState.cs` — record with `LastHash`, `LastChangeUtc`, `PrevChangeUtc`, `NextPollUtc`, `MissCount`, `Phase` (Discovery/Steady), `Cycle` (TimeSpan?), and methods `WithDataChange(hash, utc)`, `WithMiss(utc)`, `ComputeNextPoll(utc)` for state transitions
- [x] 3.3 Create `src/Njord.Tests/Pipeline/ModelPollStateSpec.cs` — tests: discovery→steady transition after 2 changes, cycle computation, retry backoff doubling (1/2/4/8/15 cap), 5 misses → fallback to discovery, past nextPollUtc → immediate

## 4. Scheduler — Actor

- [x] 4.1 Create `src/Njord/Pipeline/SchedulerActor.cs` — `ReceivePersistentActor` with `PersistenceId = "scheduler"`, recovery from `DataChanged` events, `IWithStash` for waiting on StreamRef, `ScheduleTellOnce` per (location, model), handles `HashResult` (compare + persist + schedule), `RefreshModel`/`RefreshLocation` (bypass), `ScheduledPoll` (offer to queue)
- [x] 4.2 Create `src/Njord.Tests/Pipeline/SchedulerActorSpec.cs` — tests: discovery phase polls every 20 min, hash change persists event and reschedules, second change computes cycle and transitions to steady, steady schedules at cycle+1min, miss increments backoff, 5 misses → discovery fallback, RefreshModel bypasses schedule, recovery restores state and schedules immediately if past due
- [x] 4.3 Register `SchedulerActor` in `src/Njord/Program.cs` via Akka.Hosting with DI dependencies

## 5. Pipeline — Stream Graph Refactoring

- [x] 5.1 Modify `src/Njord/Pipeline/PipelineActor.cs` — replace `MergeHub<PipelineCommand>` + `Source.Tick` with `Source.Queue<WeightedTarget>`, expose queue handle via `RequestPipelineQueue`/`PipelineQueueResponse` message pair, remove `ExpandStage` usage from the graph
- [x] 5.2 Add hash stage after MQTT publish in `src/Njord/Pipeline/PipelineActor.cs` — synchronous `Select` computing `ForecastDataHash.Compute()`, followed by built-in `Ask<Ack>` flow to SchedulerActor (5s timeout)
- [x] 5.3 Update `src/Njord.Tests/Pipeline/PollPipelineSpec.cs` — adapt existing integration tests to the new Source.Queue entry point and hash+Ask stages

## 6. Pipeline — Command Cleanup

- [x] 6.1 Remove `PollAll` variant from `src/Njord/Pipeline/PipelineCommand.cs` — keep `RefreshLocation` and `RefreshModel` only
- [x] 6.2 Update `src/Njord/Pipeline/ExpandStage.cs` — remove `PollAll` case, keep expand logic for `RefreshLocation`/`RefreshModel` (used by SchedulerActor internally to resolve commands to targets)
- [x] 6.3 Delete `src/Njord/Pipeline/TickSource.cs` — no longer used
- [x] 6.4 Update `src/Njord.Tests/Pipeline/ExpandStageSpec.cs` — remove PollAll tests, verify RefreshLocation/RefreshModel still work

## 7. Actor Lifecycle Wiring

- [x] 7.1 Update actor startup order in `src/Njord/Program.cs` — EgressActor → PipelineActor → SchedulerActor, with SchedulerActor depending on PipelineActor's queue readiness
- [x] 7.2 Update `src/Njord/Egress/MqttEgressActor.cs` — no changes needed; egress does not send refresh commands currently — route `RefreshModel`/`RefreshLocation` commands (from HA birth events) to the SchedulerActor instead of the PipelineActor's MergeHub

## 8. Configuration

- [x] 8.1 Add `DiscoveryInterval` (default 20 min) and `RetryBackoffMax` (default 15 min) to `src/Njord/Configuration/NjordOptions.cs`
- [x] 8.2 Update `src/Njord/Configuration/NjordOptionsValidator.cs` — validate DiscoveryInterval is positive, update budget projection to use worst-case discovery rate (all models at discovery interval) for the first 24h
- [x] 8.3 Add persistence path configuration `PersistencePath` (default `data/njord-journal.db`) to NjordOptions

## 9. Validation

- [x] 9.1 Run all tests: `dotnet run --project Njord.Tests/Njord.Tests.csproj` from `src/`
- [x] 9.2 Run slopwatch: `dotnet slopwatch` from repo root
