## 1. BudgetTracker FunkArr alignment

- [x] 1.1 Refactor `src/Njord.Budget/BudgetTrackerState.cs`: change `Empty(TimeProvider)` method to `static readonly BudgetTrackerState Empty = new(0, 0, 0, 0, 0)` field. Move `FromPersistence` from `BudgetTrackerStateExtensions` to a static method on `BudgetTrackerState`. Add `ProcessCommand(RecordApiCall cmd, DateTimeOffset now)` returning `(BudgetTrackerState State, ApiCallRecorded Event)` tuple.
- [x] 1.2 Refactor `src/Njord.Budget/BudgetTracker.cs`: initialize `_state` from `BudgetTrackerState.Empty`, set month/day from TimeProvider in constructor. Use `ProcessCommand` in `OnRecordApiCall` handler. Update `Recover<SnapshotOffer>` to call `BudgetTrackerState.FromPersistence(...)` (static method on record).
- [x] 1.3 Update `src/Njord.Budget.Tests/BudgetTrackerStateSpec.cs` and `src/Njord.Budget.Tests/BudgetTrackerSpec.cs`: adapt tests to new `Empty` field API and `ProcessCommand` pattern. Verify persistence roundtrip via `GetPersistenceState()` → `FromPersistence()`.

## 2. PollScheduler snapshot persistence

- [x] 2.1 Create `src/Njord.Persistence/Events/Pipeline/PersistedPollSchedulerState.cs`: extend-only DTO with `[JsonProperty]` string keys for the model poll states dictionary. Follow existing persistence record conventions.
- [x] 2.2 Add `GetPersistenceState()` and static `FromPersistence()` to `src/Njord.Pipeline/PollSchedulerState.cs`. Wire `Recover<SnapshotOffer>` in `src/Njord.Pipeline/PollScheduler.cs` to call `PollSchedulerState.FromPersistence()` instead of the current no-op. Add `SaveSnapshot` call after event persistence (with configurable interval).
- [x] 2.3 Add persistence roundtrip test in `src/Njord.Persistence.Tests/` for `PersistedPollSchedulerState` (Verify wire-format snapshot). Update `src/Njord.Pipeline.Tests/PollSchedulerSnapshotSpec.cs` to verify real snapshot recovery.

## 3. SensorHub state extraction

- [x] 3.1 Create `src/Njord.SensorHub/SensorHubState.cs`: `SensorHubState` record with `ImmutableDictionary<(string, SensorKind, string), SensorReading>`, `Empty` static readonly field, and `SensorHubStateExtensions` with `Apply`, `Validate`, `GetSnapshot`, `Expire` methods. Extract logic from current `SensorHub.cs` handlers.
- [x] 3.2 Refactor `src/Njord.SensorHub/SensorHub.cs` to thin shell: `_state` field initialized from `SensorHubState.Empty`, handlers delegate to state extensions.
- [x] 3.3 Create `src/Njord.SensorHub.Tests/SensorHubStateSpec.cs`: state-only unit tests for `Apply`, `Validate`, `GetSnapshot` (with fresh/expired readings), `Expire`. No ActorSystem needed.

## 4. Stream actor cleanup

- [x] 4.1 Reorder fields in `src/Njord.Pipeline/ForecastPipeline.cs`: constants/statics → nested message records → injected deps → runtime refs → `_log`. Ensure `WaitingForGateway` and `EnterReady` each register all handlers for their phase in one place.
- [x] 4.2 Reorder fields in `src/Njord.OpenMeteo/OpenMeteoGateway.cs`: constants/statics → nested message records → injected deps → runtime refs → `_log`. Verify `MaterializeStreams` is the single stream setup entry point.
- [x] 4.3 Reorder fields in `src/Njord.Mqtt/MqttConnection.cs`: constants/statics → nested message records → injected deps → runtime refs → `_log`. Verify `Ready` registers all handlers in one place.

## 5. Validation

- [x] 5.1 Build and run all affected test projects:
  ```
  dotnet build Njord.slnx
  dotnet run --project Njord.Budget.Tests/Njord.Budget.Tests.csproj
  dotnet run --project Njord.Pipeline.Tests/Njord.Pipeline.Tests.csproj
  dotnet run --project Njord.SensorHub.Tests/Njord.SensorHub.Tests.csproj
  dotnet run --project Njord.Persistence.Tests/Njord.Persistence.Tests.csproj
  dotnet run --project Njord.OpenMeteo.Tests/Njord.OpenMeteo.Tests.csproj
  dotnet run --project Njord.Mqtt.Tests/Njord.Mqtt.Tests.csproj
  dotnet run --project Njord.Architecture.Tests/Njord.Architecture.Tests.csproj
  ```
- [x] 5.2 Run full solution test loop to catch any cross-project regressions.
