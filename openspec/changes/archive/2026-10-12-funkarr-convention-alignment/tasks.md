## 1. Actor logging — inline readonly init

- [x] 1.1 Fix `PollScheduler`: `private ILoggingAdapter _log = null!;` → `private readonly ILoggingAdapter _log = Context.GetLogger();`, remove `_log = Context.GetLogger();` from `PreStart()`
- [x] 1.2 Fix `ForecastPipeline`: same pattern
- [x] 1.3 Fix `GrpcConsumer`: same pattern
- [x] 1.4 Fix `OpenMeteoGateway`: same pattern
- [x] 1.5 Fix `SensorHub`: same pattern
- [x] 1.6 Fix `MqttConnection`: same pattern
- [x] 1.7 Fix `MqttStatePublisher`: same pattern
- [x] 1.8 Fix `BudgetTracker`: add `private readonly ILoggingAdapter _log = Context.GetLogger();` field, replace inline `Context.GetLogger()` calls
- [x] 1.9 Build: `dotnet build Njord.slnx`
- [x] 1.10 Run all tests to verify no regressions

## 2. Actor/state naming

- [x] 2.1 Rename `SchedulerState` → `PollSchedulerState` in `src/Njord.Pipeline/SchedulerState.cs`
- [x] 2.2 Rename `SchedulerStateExtensions` → `PollSchedulerStateExtensions` in same file
- [x] 2.3 Rename file `SchedulerState.cs` → `PollSchedulerState.cs`
- [x] 2.4 Update all references in `src/Njord.Pipeline/` and `src/Njord.Pipeline.Tests/`
- [x] 2.5 Rename `MqttState` actor → `MqttStatePublisher` in `src/Njord.Mqtt/MqttState.cs`
- [x] 2.6 Rename file `MqttState.cs` → `MqttStatePublisher.cs`
- [x] 2.7 Rename `IMqttState` → `IMqttStatePublisher` in `src/Njord.Core/Actors/ActorKeys.cs`
- [x] 2.8 Update all references across `src/Njord.Mqtt/`, `src/Njord/`, `src/Njord.Mqtt.Tests/`, and any other consumers
- [x] 2.9 Build and run affected tests

## 3. DtoMapping dissolution

- [x] 3.1 Move `BudgetTrackerDtoMapping.ToSnapshot()` logic into `BudgetTrackerStateExtensions.GetPersistenceState()` — build `PersistedBudgetTrackerState` directly
- [x] 3.2 Move `BudgetTrackerDtoMapping.ToDto()` into `BudgetTrackerStateExtensions` as `ToPersistedEvent(int weight, DateTimeOffset utc)`
- [x] 3.3 Move `BudgetTrackerDtoMapping.ToDomain()` into `BudgetTrackerStateExtensions` as `FromPersistedEvent(ApiCallRecorded dto)`
- [x] 3.4 Update `BudgetTracker.cs` to use state extensions instead of mapping class
- [x] 3.5 Delete `src/Njord.Budget/BudgetTrackerDtoMapping.cs`
- [x] 3.6 Move `SchedulerDtoMapping.ToDto()` into `PollSchedulerStateExtensions` as `ToPersistedEvent(DataChanged evt)`
- [x] 3.7 Move `SchedulerDtoMapping.ToDomain()` into `PollSchedulerStateExtensions` as `FromPersistedEvent(PersistedDataChanged dto)`
- [x] 3.8 Update `PollScheduler.cs` to use state extensions instead of mapping class
- [x] 3.9 Delete `src/Njord.Pipeline/SchedulerDtoMapping.cs`
- [x] 3.10 Build and run Budget + Pipeline tests

## 4. Message naming — query responses drop Query prefix

- [x] 4.1 Rename `QueryBudgetUsageResponse` → `BudgetUsageResponse`, `QueryBudgetUsageCompleted` → `BudgetUsageResult` in `src/Njord.Messages/Pipeline/QueryBudgetUsage.cs`
- [x] 4.2 Rename `QueryPollStatesResponse` → `PollStatesResponse`, `QueryPollStatesCompleted` → `PollStatesResult` in `src/Njord.Messages/Pipeline/QueryPollStates.cs`
- [x] 4.3 Rename `QuerySensorSnapshotResponse` → `SensorSnapshotResponse`, `QuerySensorSnapshotCompleted` → `SensorSnapshotResult`, `QuerySensorSnapshotNotAvailable` → `SensorSnapshotNotAvailable` in `src/Njord.Messages/Sensors/`
- [x] 4.4 Rename `QueryForecastResponse` → `ForecastResponse`, `QueryForecastCompleted` → `ForecastResult`, `QueryForecastNotAvailable` → `ForecastNotAvailable` in `src/Njord.Messages/Snapshots/QueryForecast.cs`
- [x] 4.5 Update all references across production and test code
- [x] 4.6 Build and run all tests

## 5. Message naming — completed/failed match command name

- [x] 5.1 Rename `ForecastFetched` → `FetchForecastCompleted` in `src/Njord.Messages/Forecast/ForecastFetched.cs`, rename file
- [x] 5.2 Rename `ForecastFetchFailed` → `FetchForecastFailed` in `src/Njord.Messages/Forecast/ForecastFetchFailed.cs`, rename file
- [x] 5.3 Add abstract `FetchForecastResponse` base, make `FetchForecastCompleted` and `FetchForecastFailed` extend it
- [x] 5.4 Rename `ForecastStreamReady` → `RequestForecastStreamCompleted` in `src/Njord.Messages/Forecast/RequestForecastStream.cs`
- [x] 5.5 Rename `PipelineOutputResponse` → `RequestPipelineOutputCompleted` in `src/Njord.Core/StreamWiringMessages.cs`
- [x] 5.6 Rename `PipelineOutputFailed` → `RequestPipelineOutputFailed` in same file
- [x] 5.7 Add abstract `RequestPipelineOutputResponse` base for the two variants
- [x] 5.8 Update all references across production and test code
- [x] 5.9 Build and run all tests

## 6. Response hierarchy completion — add Failed variants

- [x] 6.1 Add `TriggerImmediatePollFailed(Exception Cause)` to `TriggerImmediatePollResponse` hierarchy
- [x] 6.2 Add `BudgetUsageFailed(Exception Cause)` to `BudgetUsageResponse` hierarchy
- [x] 6.3 Add `PollStatesFailed(Exception Cause)` to `PollStatesResponse` hierarchy
- [x] 6.4 Build: `dotnet build Njord.slnx`

## 7. Message file splitting

- [x] 7.1 Split `src/Njord.Messages/Mqtt/MqttMessages.cs` into `MqttConnected.cs`, `SubscribeInbound.cs`, `MqttInboundMessage.cs`
- [x] 7.2 Split `src/Njord.Messages/Sensors/SensorHubMessages.cs` into `UpdateReading.cs`, `QuerySensorSnapshot.cs` (with its response hierarchy)
- [x] 7.3 Delete the old multi-record files
- [x] 7.4 Build: `dotnet build Njord.slnx`

## 8. Documentation

- [x] 8.1 Update CLAUDE.md: change "Pattern B (co-located with actors)" → "Pattern A (dedicated Njord.Messages project)"
- [x] 8.2 Update AGENTS.md: add actor logging convention and *State naming rule
- [x] 8.3 Update AGENTS.md test count (666)

## 9. Validation

- [x] 9.1 Full build: `dotnet build Njord.slnx` — 0 errors, 0 warnings
- [x] 9.2 Run all test projects sequentially — 666 tests, 0 failures
- [x] 9.3 Run architecture tests — 35 tests, all green
- [x] 9.4 Grep for remaining old type names — none found
