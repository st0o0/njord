# Design: persistence-simplification

## D1: ApiCallRecorded — long Ticks → DateTimeOffset Utc

```csharp
// Before
public sealed record ApiCallRecorded(int Weight, long Ticks);

// After
public sealed record ApiCallRecorded(int Weight, DateTimeOffset Utc);
```

`BudgetTrackerState.FromPersistedEvent` currently returns
`(int Weight, DateTimeOffset Utc)` by converting `dto.Ticks` →
`DateTimeOffset`. After this change, `dto.Utc` is already a DateTimeOffset —
the tuple decomposition can use it directly, or the method can simplify to
just returning the event fields.

`BudgetTracker.Apply(ApiCallRecorded evt, DateTimeOffset now)` — the second
`now` parameter was for the current time (different from the event's recorded
time). Check if `now` is still needed separately or if `evt.Utc` covers it.

## D2: PersistedDataChanged — long Ticks → DateTimeOffset Utc

```csharp
// Before
public sealed record PersistedDataChanged(string Location, string ModelId, int Hash, long Ticks);

// After
public sealed record PersistedDataChanged(string Location, string ModelId, int Hash, DateTimeOffset Utc);
```

`ToPersistedEvent` simplifies:
```csharp
// Before
new PersistedDataChanged(evt.Location, evt.ModelId, evt.Hash, evt.Utc.UtcTicks)
// After
new PersistedDataChanged(evt.Location, evt.ModelId, evt.Hash, evt.Utc)
```

`FromPersistedEvent` simplifies:
```csharp
// Before
new DataChanged(dto.Location, dto.ModelId, dto.Hash, new DateTimeOffset(dto.Ticks, TimeSpan.Zero))
// After
new DataChanged(dto.Location, dto.ModelId, dto.Hash, dto.Utc)
```

## D3: PersistedBudgetTrackerState → positional record

```csharp
// Before
public sealed record PersistedBudgetTrackerState
{
    [JsonProperty("month")] public int Month { get; init; }
    [JsonProperty("day")] public int Day { get; init; }
    [JsonProperty("monthly")] public long MonthlyUsed { get; init; }
    [JsonProperty("daily")] public long DailyUsed { get; init; }
}

// After
public sealed record PersistedBudgetTrackerState(
    int Month, int Day, long MonthlyUsed, long DailyUsed);
```

`GetPersistenceState` changes from init-property syntax to positional:
```csharp
// Before
new() { Month = state.CurrentMonth, Day = state.CurrentDay, ... }
// After
new(state.CurrentMonth, state.CurrentDay, state.MonthlyUsed, state.DailyUsed)
```

## D4: PersistedPollSchedulerState + PersistedModelPollEntry → positional records

```csharp
// PersistedPollSchedulerState stays positional (already is)

// Before
public sealed record PersistedModelPollEntry
{
    [JsonProperty("key")] public string Key { get; init; } = "";
    [JsonProperty("hash")] public int? LastHash { get; init; }
    [JsonProperty("changed")] public long? LastChangeUtcTicks { get; init; }
    [JsonProperty("prev")] public long? PrevChangeUtcTicks { get; init; }
    [JsonProperty("miss")] public int MissCount { get; init; }
    [JsonProperty("phase")] public int Phase { get; init; }
    [JsonProperty("cycle")] public long? CycleTicks { get; init; }
    [JsonProperty("tfc")] public int TransientFailureCount { get; init; }
}

// After
public sealed record PersistedModelPollEntry(
    string Key,
    int? LastHash,
    DateTimeOffset? LastChangeUtc,
    DateTimeOffset? PrevChangeUtc,
    int MissCount,
    int Phase,
    TimeSpan? Cycle,
    int TransientFailureCount);
```

Key changes:
- `long? LastChangeUtcTicks` → `DateTimeOffset? LastChangeUtc`
- `long? PrevChangeUtcTicks` → `DateTimeOffset? PrevChangeUtc`
- `long? CycleTicks` → `TimeSpan? Cycle` (was a duration, not a timestamp)
- Remove all `[JsonProperty]` and init-property style

`GetPersistenceState` loses all Ticks conversion:
```csharp
new PersistedModelPollEntry(
    kvp.Key, s.LastHash, s.LastChangeUtc, s.PrevChangeUtc,
    s.MissCount, (int)s.Phase, s.Cycle, s.TransientFailureCount)
```

## D5: Update Verify snapshots

Wire-format snapshot tests (`Njord.Persistence.Tests`) use Verify `.txt`
baseline files. These will need re-approval since the JSON shape changes
(property names, DateTimeOffset format). Re-run tests, accept new baselines.
