## 1. ApiCallRecorded — long Ticks → DateTimeOffset Utc

- [x] 1.1 In `src/Njord.Persistence/Events/Budget/ApiCallRecorded.cs`: change `long Ticks` to `DateTimeOffset Utc`. Remove `Newtonsoft.Json` using if no longer needed.
- [x] 1.2 In `src/Njord.Budget/BudgetTrackerState.cs`: update `FromPersistedEvent` — `dto.Utc` is now directly a `DateTimeOffset`, remove Ticks conversion. Update `Apply(ApiCallRecorded evt, DateTimeOffset now)` callers if the signature simplifies.
- [x] 1.3 In `src/Njord.Budget/BudgetTracker.cs`: update event construction in `Persist` — pass `DateTimeOffset` directly instead of `.UtcTicks`.
- [x] 1.4 Update tests in `src/Njord.Budget.Tests/` for the new record shape.
- [x] 1.5 Build: `dotnet build Njord.slnx` — zero errors.

**Validation:**
```powershell
dotnet run --project Njord.Budget.Tests/Njord.Budget.Tests.csproj
```

## 2. PersistedDataChanged — long Ticks → DateTimeOffset Utc

- [x] 2.1 In `src/Njord.Persistence/Events/Pipeline/PersistedDataChanged.cs`: change `long Ticks` to `DateTimeOffset Utc`.
- [x] 2.2 In `src/Njord.Pipeline/PollSchedulerState.cs`: simplify `ToPersistedEvent` — pass `evt.Utc` directly. Simplify `FromPersistedEvent` — `dto.Utc` is already DateTimeOffset.
- [x] 2.3 In `src/Njord.Pipeline/PollScheduler.cs`: update event construction if needed.
- [x] 2.4 Update tests in `src/Njord.Pipeline.Tests/` for the new record shape.
- [x] 2.5 Build: `dotnet build Njord.slnx` — zero errors.

**Validation:**
```powershell
dotnet run --project Njord.Pipeline.Tests/Njord.Pipeline.Tests.csproj
```

## 3. PersistedBudgetTrackerState → positional record

- [x] 3.1 In `src/Njord.Persistence/Events/Budget/PersistedBudgetTrackerState.cs`: convert from init-property + `[JsonProperty]` to positional record `PersistedBudgetTrackerState(int Month, int Day, long MonthlyUsed, long DailyUsed)`. Remove `using Newtonsoft.Json`.
- [x] 3.2 In `src/Njord.Budget/BudgetTrackerState.cs`: update `GetPersistenceState` from init-property syntax to positional constructor. Update `FromPersistence` if needed.
- [x] 3.3 Update tests in `src/Njord.Budget.Tests/`.
- [x] 3.4 Build: `dotnet build Njord.slnx` — zero errors.

**Validation:**
```powershell
dotnet run --project Njord.Budget.Tests/Njord.Budget.Tests.csproj
```

## 4. PersistedModelPollEntry → positional record with DateTimeOffset/TimeSpan

- [x] 4.1 In `src/Njord.Persistence/Events/Pipeline/PersistedPollSchedulerState.cs`: convert `PersistedModelPollEntry` from init-property + `[JsonProperty]` to positional record. Change `long?` ticks fields to `DateTimeOffset?` and `TimeSpan?`. Remove `using Newtonsoft.Json`.
- [x] 4.2 In `src/Njord.Pipeline/PollSchedulerState.cs`: update `GetPersistenceState` — remove all `.UtcTicks` / `.Ticks` conversions, pass values directly. Update `FromPersistence` — remove all `new DateTimeOffset(ticks, TimeSpan.Zero)` conversions.
- [x] 4.3 Update tests in `src/Njord.Pipeline.Tests/` for the new record shape.
- [x] 4.4 Build: `dotnet build Njord.slnx` — zero errors.

**Validation:**
```powershell
dotnet run --project Njord.Pipeline.Tests/Njord.Pipeline.Tests.csproj
```

## 5. Update Verify snapshots

- [x] 5.1 Run `src/Njord.Persistence.Tests/` — tests will fail because Verify baselines have the old JSON shape.
- [x] 5.2 Accept new Verify baselines (`.verified.txt` files) — the JSON now has ISO-8601 timestamps instead of numeric ticks, and property names match the positional parameter names.
- [x] 5.3 Run again to confirm all pass: `dotnet run --project Njord.Persistence.Tests/Njord.Persistence.Tests.csproj` — zero failures.

**Validation:**
```powershell
dotnet run --project Njord.Persistence.Tests/Njord.Persistence.Tests.csproj
```

## 6. Full validation

- [x] 6.1 Run full test suite sequentially across all `Njord.*Tests` projects — zero failures.

**Validation:**
```powershell
dotnet run --project Njord.Architecture.Tests/Njord.Architecture.Tests.csproj
```

## 7. Update AGENTS.md

- [x] 7.1 Update `AGENTS.md` persistence conventions: positional records, `DateTimeOffset` for timestamps / `TimeSpan` for durations, no `[JsonProperty]`, no `long` ticks.
