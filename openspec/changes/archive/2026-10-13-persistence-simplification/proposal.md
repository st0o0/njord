# Persistence Simplification — FunkArr positional records, native DateTimeOffset

## Problem

njord's persistence records deviate from FunkArr's style in three ways that
add boilerplate without benefit:

1. **`long Ticks` instead of `DateTimeOffset`** — `ApiCallRecorded` stores
   `long Ticks`, `PersistedDataChanged` stores `long Ticks`,
   `PersistedModelPollEntry` stores `long? LastChangeUtcTicks` etc. Every
   read/write needs `new DateTimeOffset(ticks, TimeSpan.Zero)` /
   `.UtcTicks` conversion. FunkArr uses `DateTimeOffset` directly —
   Newtonsoft serializes it natively as ISO-8601.

2. **`[JsonProperty]` init-property style on snapshots** —
   `PersistedBudgetTrackerState` and `PersistedModelPollEntry` use
   `{ [JsonProperty("x")] public T X { get; init; } }` instead of
   positional records. FunkArr uses positional records without
   `[JsonProperty]` everywhere — Newtonsoft matches by property name.

3. **`ToPersistedEvent`/`FromPersistedEvent` boilerplate** — manual
   conversion between Messages `DataChanged` and Persistence
   `PersistedDataChanged` exists only because the types differ by Ticks vs
   DateTimeOffset. With native DateTimeOffset, the event fields align and
   conversion simplifies dramatically.

## Proposed changes

### Events → positional records with DateTimeOffset

```csharp
// Before
public sealed record ApiCallRecorded(int Weight, long Ticks);
public sealed record PersistedDataChanged(string Location, string ModelId, int Hash, long Ticks);

// After
public sealed record ApiCallRecorded(int Weight, DateTimeOffset Utc);
public sealed record PersistedDataChanged(string Location, string ModelId, int Hash, DateTimeOffset Utc);
```

### Snapshots → positional records, drop [JsonProperty]

```csharp
// Before
public sealed record PersistedBudgetTrackerState
{
    [JsonProperty("month")] public int Month { get; init; }
    ...
}

// After
public sealed record PersistedBudgetTrackerState(
    int Month, int Day, long MonthlyUsed, long DailyUsed);
```

Same for `PersistedModelPollEntry` — positional record with DateTimeOffset
fields replacing long ticks fields.

### Simplify conversion methods

`FromPersistedEvent` / `ToPersistedEvent` simplify since fields now match
directly. `FromPersistence` / `GetPersistenceState` lose all Ticks conversion
boilerplate.

## Non-goals

- Changing the three-tier state model structure (Internal → Snapshot →
  Persisted). Only the persisted tier's record style changes.
- Migrating existing journal data. This is a 0.x breaking change
  (`refactor!:`). SQLite journals are local dev data.
- Changing the Newtonsoft serializer configuration. DateTimeOffset
  serialization works out of the box.

## API-budget impact

None — no change to polling or API calls.

## Risks

- **Journal compatibility break** — existing persisted snapshots and events
  won't deserialize after this change. Acceptable at 0.x with local SQLite.
  Delete `data/njord-journal.db` to start fresh.
