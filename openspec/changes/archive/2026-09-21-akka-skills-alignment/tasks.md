## 1. Shared Infrastructure

- [x] 1.1 Deduplicate `Ack` — create a single `Ack` record in a shared location (e.g. `src/Njord/Actors/SharedMessages.cs`), remove the duplicate definitions from `src/Njord/Pipeline/SchedulerMessages.cs` and `src/Njord/Grpc/SnapshotMessages.cs`, update all `using` references
- [x] 1.2 Run full test suite to verify Ack dedup: `dotnet run --project Njord.Tests/Njord.Tests.csproj`

## 2. BudgetTrackerActor — State Pattern + Messages

- [x] 2.1 Create `src/Njord/Pipeline/BudgetTrackerState.cs` — immutable `record BudgetTrackerState(int CurrentMonth, int CurrentDay, long MonthlyUsed, long DailyUsed)` with `Apply(ApiCallRecorded)`, `GetSnapshot()` → `BudgetUsageResult`, `GetPersistenceState()` → `BudgetTrackerSnapshotDto`, `FromPersistence(BudgetTrackerSnapshotDto)` extension methods
- [x] 2.2 Refactor `src/Njord/Pipeline/BudgetTrackerActor.cs` — replace 5 mutable fields with single `_state` record, delegate all logic to state extensions, keep actor as thin shell (Persist, Tell, SaveSnapshot only)
- [x] 2.3 Rename `GetBudgetUsage` → `QueryBudgetUsage` in `src/Njord/Pipeline/BudgetTrackerActor.cs`, update all callers (grep for `GetBudgetUsage`)
- [x] 2.4 Add response hierarchy — `abstract record BudgetResponse`, `sealed record BudgetUsageResult(...) : BudgetResponse`, `sealed record BudgetResponseFailed(Exception Cause) : BudgetResponse`, update actor to catch + respond with Failed
- [x] 2.5 Create `src/Njord.Tests/Pipeline/BudgetTrackerStateSpec.cs` — sealed class, `[Fact(Timeout = 5000)]`, BDD methods: tests for `Apply` (single call, month rollover, day rollover), `GetSnapshot`, `GetPersistenceState`, `FromPersistence` roundtrip, initial empty state
- [x] 2.6 Update `src/Njord.Tests/Pipeline/BudgetTrackerActorSpec.cs` — adapt to renamed messages and new response types
- [x] 2.7 Run full test suite: `dotnet run --project Njord.Tests/Njord.Tests.csproj`

## 3. ForecastSnapshotActor — State Pattern + Messages

- [x] 3.1 Create `src/Njord/Grpc/ForecastSnapshotState.cs` — immutable `record ForecastSnapshotState(ImmutableDictionary<string, ModelForecast> Forecasts, int UpdatesSinceSnapshot)` with `Apply(UpdateForecast)`, `GetSnapshot(key)`, `GetPersistenceState()` → `ForecastSnapshotDto`, `FromPersistence(ForecastSnapshotDto)` extensions
- [x] 3.2 Refactor `src/Njord/Grpc/ForecastSnapshotActor.cs` — replace mutable dict + counter with state record
- [x] 3.3 Rename `GetForecast` → `QueryForecast`, `GetAllForecasts` → `QueryAllForecasts` in `src/Njord/Grpc/SnapshotMessages.cs`, update all callers
- [x] 3.4 Add response hierarchy — `abstract record ForecastQueryResponse`, subtypes: `ForecastFound(ModelForecast)`, `ForecastNotFound(string ModelKey)`, `ForecastQueryFailed(Exception Cause)`. Same for all-forecasts: `AllForecastsResult(...)`, `AllForecastsFailed(Exception Cause)`
- [x] 3.5 Create `src/Njord.Tests/Grpc/ForecastSnapshotStateSpec.cs` — pure function tests for Apply, GetSnapshot, GetPersistenceState, FromPersistence roundtrip, empty state, not-found key
- [x] 3.6 Update `src/Njord.Tests/Grpc/ForecastSnapshotActorSpec.cs` — adapt to renamed messages and response types
- [x] 3.7 Run full test suite: `dotnet run --project Njord.Tests/Njord.Tests.csproj`

## 4. EnrichmentSnapshotActor — State Pattern + Messages

- [x] 4.1 Create `src/Njord/Grpc/EnrichmentSnapshotState.cs` — immutable `record EnrichmentSnapshotState(ImmutableDictionary<string, object> Enrichments, int UpdatesSinceSnapshot)` with `Apply(UpdateEnrichment)`, `GetSnapshot(key)`, `GetPersistenceState()` → `EnrichmentSnapshotDto`, `FromPersistence(EnrichmentSnapshotDto)` extensions
- [x] 4.2 Refactor `src/Njord/Grpc/EnrichmentSnapshotActor.cs` — replace mutable dict + counter with state record
- [x] 4.3 Rename `GetEnrichment` → `QueryEnrichment`, `GetAllEnrichments` → `QueryAllEnrichments` in `src/Njord/Grpc/SnapshotMessages.cs`, update all callers
- [x] 4.4 Add response hierarchy — `abstract record EnrichmentQueryResponse`, subtypes: `EnrichmentFound(object Result)`, `EnrichmentNotFound(string Key)`, `EnrichmentQueryFailed(Exception Cause)`. Same for all-enrichments.
- [x] 4.5 Create `src/Njord.Tests/Grpc/EnrichmentSnapshotStateSpec.cs` — pure function tests
- [x] 4.6 Update `src/Njord.Tests/Grpc/EnrichmentSnapshotActorSpec.cs` — adapt to renamed messages and response types
- [x] 4.7 Run full test suite: `dotnet run --project Njord.Tests/Njord.Tests.csproj`

## 5. ForecastHistoryActor — State Pattern

- [x] 5.1 Create `src/Njord/Enrichment/ForecastHistoryState.cs` — immutable state record wrapping `ForecastHistory` with `Apply(ForecastRecordDto)`, `GetSnapshot()` → `HistoryResponse`, `GetPersistenceState()` → `ForecastHistorySnapshotDto`, `FromPersistence(ForecastHistorySnapshotDto)` extensions
- [x] 5.2 Refactor `src/Njord/Enrichment/ForecastHistoryActor.cs` — replace mutable `_history` + `_eventsSinceSnapshot` with state record
- [x] 5.3 Add response hierarchy for `QueryHistory` — `abstract record HistoryQueryResponse`, subtypes: `HistoryResult(ForecastHistory)`, `HistoryQueryFailed(Exception Cause)`. Update caller (EnrichmentActor)
- [x] 5.4 Create `src/Njord.Tests/Enrichment/ForecastHistoryStateSpec.cs` — pure function tests for Apply (add record, retention cutoff), GetSnapshot, GetPersistenceState, FromPersistence roundtrip
- [x] 5.5 Update `src/Njord.Tests/Enrichment/ForecastHistoryActorSpec.cs` — adapt to response types
- [x] 5.6 Run full test suite: `dotnet run --project Njord.Tests/Njord.Tests.csproj`

## 6. SchedulerActor — State Pattern + Messages

- [x] 6.1 Create `src/Njord/Pipeline/SchedulerState.cs` — immutable state record for the poll-state dictionary only (`ImmutableDictionary<string, ModelPollState>`) with `Apply(DataChanged)`, `GetSnapshot()` → `PollStatesSnapshot`, `GetPersistenceState()` (if snapshots added) or event-only recovery via `Apply`
- [x] 6.2 Refactor `src/Njord/Pipeline/SchedulerActor.cs` — extract persistent `_states` into state record, keep transient fields (`_queue`, `_sourceReceived`, `_lastTerminatedPipeline`, `_pipelineRetryCount`, Become phase) as mutable actor fields
- [x] 6.3 Rename `GetPollStates` → `QueryPollStates` in `src/Njord/Pipeline/SchedulerMessages.cs`, update all callers
- [x] 6.4 Add response hierarchy for poll states query — `abstract record SchedulerQueryResponse`, subtypes: `PollStatesResult(...)`, `SchedulerQueryFailed(Exception Cause)`
- [x] 6.5 Create `src/Njord.Tests/Pipeline/SchedulerStateSpec.cs` — pure function tests for Apply(DataChanged), GetSnapshot, initial empty state, multiple model updates
- [x] 6.6 Update `src/Njord.Tests/Pipeline/SchedulerActorSpec.cs` — adapt to renamed messages and response types
- [x] 6.7 Run full test suite: `dotnet run --project Njord.Tests/Njord.Tests.csproj`

## 7. SensorHub Message Rename

- [x] 7.1 Rename `GetSnapshot` → `QuerySensorSnapshot` in `src/Njord/Domain/Sensors/SensorHubMessages.cs`, update all callers
- [x] 7.2 Update `src/Njord.Tests/Sensors/SensorHubActorSpec.cs` — adapt to renamed message
- [x] 7.3 Run full test suite: `dotnet run --project Njord.Tests/Njord.Tests.csproj`

## 8. Documentation + Final Validation

- [x] 8.1 Update `CLAUDE.md` — change "Pattern A (dedicated Messages project)" to "Pattern B (co-located with actors)" in the Decisions or Conventions section
- [x] 8.2 Run `dotnet slopwatch` from repo root to verify no quality regressions
- [x] 8.3 Run `dotnet format --verify-no-changes` from `src/` to verify formatting
- [x] 8.4 Run full test suite one final time: `dotnet run --project Njord.Tests/Njord.Tests.csproj`
