## 1. NjordHealthState → NjordHealthMonitor

- [x] 1.1 Create `MqttHealthSnapshot`, `PollHealthSnapshot`, `BudgetHealthSnapshot` immutable records and `NjordHealthMonitor` class with `Volatile.Read`/`Write` in `src/Njord.Core/Health/NjordHealthMonitor.cs`. Delete `src/Njord.Core/Health/NjordHealthState.cs`.
- [x] 1.2 Update `src/Njord.Mqtt/MqttConnection.cs`: replace `NjordHealthState.SetMqttConnected/Disconnected` with `NjordHealthMonitor.UpdateMqtt(new MqttHealthSnapshot(...))`.
- [x] 1.3 Update `src/Njord.Pipeline/PollScheduler.cs`: replace `NjordHealthState.SetLastSuccessfulPoll` with `NjordHealthMonitor.UpdatePoll(new PollHealthSnapshot(...))`.
- [x] 1.4 Update `src/Njord.Budget/BudgetTracker.cs`: replace `NjordHealthState.SetBudgetUsage` with `NjordHealthMonitor.UpdateBudget(new BudgetHealthSnapshot(...))`.
- [x] 1.5 Update `src/Njord/Configuration/CoreSetupContainer.cs`: register `NjordHealthMonitor` as singleton, set budget limits via `UpdateBudget`.
- [x] 1.6 Update `src/Njord/Health/PipelineHealthCheck.cs` and `src/Njord/Health/MqttConnectionHealthCheck.cs`: read from `NjordHealthMonitor` snapshot properties.
- [x] 1.7 Update all test files referencing `NjordHealthState` (15 files in Pipeline.Tests, Budget.Tests, Mqtt.Tests).
- [x] 1.8 Build: `dotnet build Njord.slnx` — zero errors.

**Validation:**
```powershell
dotnet run --project Njord.Pipeline.Tests/Njord.Pipeline.Tests.csproj
dotnet run --project Njord.Budget.Tests/Njord.Budget.Tests.csproj
dotnet run --project Njord.Mqtt.Tests/Njord.Mqtt.Tests.csproj
```

## 2. DataChanged → PersistedDataChanged

- [x] 2.1 Rename `DataChanged` to `PersistedDataChanged` in `src/Njord.Persistence/Events/Pipeline/DataChanged.cs`. Rename file to `PersistedDataChanged.cs`. Keep all `[JsonProperty]` wire names unchanged.
- [x] 2.2 Update `src/Njord.Pipeline/PollSchedulerState.cs`: update `ToPersistedEvent`/`FromPersistedEvent` return/parameter types. Remove the `using DataChanged = ...` and `using PersistedDataChanged = ...` aliases — they are no longer needed.
- [x] 2.3 Update `src/Njord.Pipeline/PollScheduler.cs`: update `Persist<PersistedDataChanged>` and `Recover<PersistedDataChanged>` references.
- [x] 2.4 Update test files in `src/Njord.Pipeline.Tests/` and `src/Njord.Persistence.Tests/`.
- [x] 2.5 Build: `dotnet build Njord.slnx` — zero errors.

**Validation:**
```powershell
dotnet run --project Njord.Pipeline.Tests/Njord.Pipeline.Tests.csproj
dotnet run --project Njord.Persistence.Tests/Njord.Persistence.Tests.csproj
```

## 3. IMqttConnection → IMqttConnectionActor

- [x] 3.1 Rename `IMqttConnection` to `IMqttConnectionActor` in `src/Njord.Core/Actors/ActorKeys.cs`.
- [x] 3.2 Update all consumers: `src/Njord.Mqtt/MqttConnection.cs`, `src/Njord.Mqtt/Configuration/MqttSetupContainer.cs`, `src/Njord.Mqtt.Tests/MqttConnectionSpec.cs`.
- [x] 3.3 Build: `dotnet build Njord.slnx` — zero errors.

**Validation:**
```powershell
dotnet run --project Njord.Mqtt.Tests/Njord.Mqtt.Tests.csproj
```

## 4. TrendAnalyzer → TrendComputer (Compute)

- [x] 4.1 Rename file `src/Njord.Compute/Analysis/TrendAnalyzer.cs` to `TrendComputer.cs`. Rename the `static class TrendAnalyzer` to `TrendComputer`. Update all internal references within Njord.Compute.
- [x] 4.2 Update consumers: grep for `TrendAnalyzer` (the static class, not the DI analyzer in Njord.Trends) across all projects and update. The DI `Njord.Trends.TrendAnalyzer` keeps its name.
- [x] 4.3 Build: `dotnet build Njord.slnx` — zero errors.

**Validation:**
```powershell
dotnet run --project Njord.Compute.Tests/Njord.Compute.Tests.csproj
dotnet run --project Njord.Trends.Tests/Njord.Trends.Tests.csproj
```

## 5. ForecastStreamFailed → RequestForecastStreamFailed

- [x] 5.1 Rename `ForecastStreamFailed` to `RequestForecastStreamFailed` in `src/Njord.Messages/Forecast/RequestForecastStream.cs`.
- [x] 5.2 Update all consumers: `src/Njord.Pipeline/ForecastPipeline.cs`, `src/Njord.OpenMeteo/OpenMeteoGateway.cs`, `src/Njord.Pipeline.Tests/PipelineConnectionSpec.cs`.
- [x] 5.3 Build: `dotnet build Njord.slnx` — zero errors.

**Validation:**
```powershell
dotnet run --project Njord.Pipeline.Tests/Njord.Pipeline.Tests.csproj
```

## 6. Snapshot interval → LastSequenceNr

- [x] 6.1 In `src/Njord.Budget/BudgetTracker.cs`: replace `_state.EventsSinceSnapshot >= SnapshotInterval` with `LastSequenceNr % SnapshotInterval == 0`. Remove the counter increment logic.
- [x] 6.2 In `src/Njord.Budget/BudgetTrackerState.cs`: remove `EventsSinceSnapshot` from the state record. Update `Apply` to not increment it. Update `GetPersistenceState`/`FromPersistence` — make the persisted field nullable with a default (extend-only: old snapshots still deserialize).
- [x] 6.3 In `src/Njord.Pipeline/PollScheduler.cs`: replace `_eventsSinceSnapshot` counter with `LastSequenceNr % SnapshotInterval == 0`. Remove the `_eventsSinceSnapshot` field.
- [x] 6.4 Update tests in `src/Njord.Budget.Tests/BudgetTrackerSpec.cs` and `src/Njord.Pipeline.Tests/PollSchedulerSnapshotSpec.cs` for the new snapshot trigger.
- [x] 6.5 Build: `dotnet build Njord.slnx` — zero errors.

**Validation:**
```powershell
dotnet run --project Njord.Budget.Tests/Njord.Budget.Tests.csproj
dotnet run --project Njord.Pipeline.Tests/Njord.Pipeline.Tests.csproj
```

## 7. Apply methods take event records

- [x] 7.1 Change `BudgetTrackerState.Apply(int weight, DateTimeOffset now)` to `Apply(ApiCallRecorded evt, DateTimeOffset now)` in `src/Njord.Budget/BudgetTrackerState.cs`. Extract weight and utc from the event record inside the method.
- [x] 7.2 Change `PollSchedulerState.Apply(string key, int hash, DateTimeOffset utc, TimeSpan interval)` to `Apply(DataChanged evt, TimeSpan discoveryInterval)` in `src/Njord.Pipeline/PollSchedulerState.cs`. Extract key, hash, utc from the message record inside.
- [x] 7.3 Update callers in `BudgetTracker.cs` and `PollScheduler.cs` to pass the event record.
- [x] 7.4 Update tests for the new signatures.
- [x] 7.5 Build: `dotnet build Njord.slnx` — zero errors.

**Validation:**
```powershell
dotnet run --project Njord.Budget.Tests/Njord.Budget.Tests.csproj
dotnet run --project Njord.Pipeline.Tests/Njord.Pipeline.Tests.csproj
```

## 8. Journal cleanup after snapshot

- [x] 8.1 In `src/Njord.Budget/BudgetTracker.cs`: add a private `MaybeSnapshot()` helper. In `SaveSnapshotSuccess` handler, add `DeleteMessages(e.Metadata.SequenceNr)` and `DeleteSnapshots(new SnapshotSelectionCriteria(e.Metadata.SequenceNr - 1))`. Add handlers for `DeleteMessagesSuccess`, `DeleteMessagesFailure`, `DeleteSnapshotsSuccess`, `DeleteSnapshotsFailure`.
- [x] 8.2 In `src/Njord.Pipeline/PollScheduler.cs`: same pattern — `MaybeSnapshot()` helper, journal/snapshot cleanup in `SaveSnapshotSuccess`, add delete result handlers.
- [x] 8.3 Update tests: verify that `SaveSnapshotSuccess` triggers `DeleteMessages` and `DeleteSnapshots`.
- [x] 8.4 Build: `dotnet build Njord.slnx` — zero errors.

**Validation:**
```powershell
dotnet run --project Njord.Budget.Tests/Njord.Budget.Tests.csproj
dotnet run --project Njord.Pipeline.Tests/Njord.Pipeline.Tests.csproj
```

## 9. Architecture test + final validation

- [x] 9.1 Run architecture tests: `dotnet run --project Njord.Architecture.Tests/Njord.Architecture.Tests.csproj` — zero failures.
- [x] 9.2 Run full test suite sequentially across all `Njord.*Tests` projects — zero failures.

**Validation:**
```powershell
dotnet run --project Njord.Architecture.Tests/Njord.Architecture.Tests.csproj
```

## 10. Update AGENTS.md

- [x] 10.1 Update `AGENTS.md`: document `NjordHealthMonitor` (replaces `NjordHealthState`), `PersistedDataChanged`, `IMqttConnectionActor`, `TrendComputer`, `RequestForecastStreamFailed`. Add conventions: "Persistent actors use `LastSequenceNr % SnapshotInterval` for snapshot scheduling, not counters" and "Journal cleanup after snapshot (`DeleteMessages` + `DeleteSnapshots` in `SaveSnapshotSuccess`)."
