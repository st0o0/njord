## Context

njord's domain layer resolves `ParameterDef` instances from strings in 51
places across 7 files, using 4 different mechanisms. The foundation for typed
access exists (`ParameterDef` as value object, `ForecastPoint` keyed by
`ParameterDef`), but the Compute layer re-discovers parameters by string on
every call. `ForecastSeries` has no querying API, leading to duplicated
time-window filtering and an identical `Mean24h` method in two files.

## Goals / Non-Goals

**Goals:**

- Every domain Compute method resolves parameters via typed static
  properties, not string literals.
- `ResolvedParameterSet` provides O(1) lookup by `ParameterDef` instead of
  O(n) linear scan by string.
- `ForecastSeries` provides `Window`, `Mean`, and `Values` methods so
  consumers don't re-implement time filtering.
- `Mean24h` exists exactly once (on `ForecastSeries`).
- `HistoryAnalyzer` and `ForecastHistory` use `ParameterDef` keys, not
  strings.

**Non-Goals:**

- Changing scoring algorithms, weights, or thresholds.
- Removing `GetByApiName(string)` from `ParameterRegistry` — the ingest
  parser needs it.
- Changing `ParameterDef`'s identity or fields.

## Decisions

### Decision 1: Static properties on ParameterRegistry, not an enum

```csharp
public static class ParameterRegistry
{
    public static ParameterDef Temperature2m { get; }
    public static ParameterDef ApparentTemperature { get; }
    public static ParameterDef WindSpeed10m { get; }
    public static ParameterDef WindGusts10m { get; }
    public static ParameterDef Precipitation { get; }
    // ... ~15 total, only those referenced in domain Compute methods
}
```

Initialised in the static constructor from the existing `BuildAll()` list.
Non-nullable — if a property's `ApiName` is not found in the built list,
the static constructor throws, failing startup.

**Why not an enum:** `ParameterDef` carries rich metadata (Unit, DeviceClass,
JsonKey, Group, Granularity). An enum would require a separate mapping layer
to get to the `ParameterDef`. Static properties give direct access to the
full object with compile-time safety on the reference.

**Why not all 73 parameters:** Only ~15 are referenced in domain Compute
methods. Adding static properties for all 73 would be noise — the rest are
accessed via `ResolvedParameterSet` iteration (for building sensor grids)
or via `GetByApiName` (for parsing API responses).

### Decision 2: HashSet-backed lookup on ResolvedParameterSet

```csharp
public sealed class ResolvedParameterSet
{
    private readonly HashSet<ParameterDef> _hourlySet;
    private readonly HashSet<ParameterDef> _dailySet;

    public ParameterDef? Get(ParameterDef param)
        => _hourlySet.TryGetValue(param, out var found) ? found
         : _dailySet.TryGetValue(param, out found) ? found
         : null;

    public bool Contains(ParameterDef param)
        => _hourlySet.Contains(param) || _dailySet.Contains(param);
}
```

**Why `HashSet.TryGetValue` instead of `Dictionary`:** `ParameterDef`
defines equality by `(ApiName, Granularity)`. We want to check "is this
parameter in the resolved set?" — that's a set membership question, not a
key-value mapping. `TryGetValue` on `HashSet<T>` returns the actual stored
instance, which may carry different metadata (Unit, DeviceClass) than the
registry's static property if the user overrides parameters.

**Why nullable return:** A parameter might not be in the resolved set if
the user excluded it via config. The caller must handle the null —
making missing parameters explicit rather than silently producing a default
score.

### Decision 3: Querying API on ForecastSeries

```csharp
public sealed record ForecastSeries(IReadOnlyList<ForecastPoint> Points)
{
    public ForecastSeries Window(DateTimeOffset from, DateTimeOffset to)
        => new(Points.Where(p => p.ValidAt >= from && p.ValidAt <= to).ToList());

    public double? Mean(ParameterDef param, DateTimeOffset from, DateTimeOffset to)
    {
        double sum = 0; int count = 0;
        foreach (var point in Points)
        {
            if (point.ValidAt < from || point.ValidAt > to) continue;
            if (point.Get(param) is not { } v) continue;
            sum += v; count++;
        }
        return count > 0 ? sum / count : null;
    }

    public IEnumerable<double> Values(ParameterDef param,
        DateTimeOffset from, DateTimeOffset to)
    { ... }
}
```

**Why on `ForecastSeries` and not a static utility:** The methods operate on
the series' own `Points` — they are natural instance methods. Putting them
elsewhere would force every caller to pass `Points` as a parameter.

**Why not LINQ:** The `Mean` method avoids allocating intermediate
collections. The hot path (every Compute call, every location, every
parameter) benefits from a single-pass loop.

### Decision 4: Migrate consumers bottom-up

**Order:**
1. Add static properties to `ParameterRegistry` (no breaking changes).
2. Add `Get`/`Contains` to `ResolvedParameterSet` (no breaking changes).
3. Add `Window`/`Mean`/`Values` to `ForecastSeries` (no breaking changes).
4. Migrate each Compute method one at a time — each is independently
   compilable and testable.
5. Delete `Mean24h` copies after both `IndexResult` and `EnergyResult` are
   migrated.

This order means every step compiles and tests pass — no big-bang migration.

### Decision 5: HistoryAnalyzer switches to ParameterDef keys

The `HistoryAnalyzer` API changes from:

```csharp
double? ComputeBias(ForecastHistory history, string paramApiName, ...)
```

to:

```csharp
double? ComputeBias(ForecastHistory history, ParameterDef param, ...)
```

`ForecastHistory`'s internal storage changes from
`Dictionary<string, double?>` to `Dictionary<ParameterDef, double?>`.
The `ForecastHistoryActor` that populates it extracts `ParameterDef` keys
from the `ModelForecast` directly (which already uses `ParameterDef`-keyed
`ForecastPoint`).

## Risks / Trade-offs

- **[Static properties require maintenance]** Adding a new parameter to the
  registry that domain Compute methods need requires adding a static
  property. This is deliberate — it forces an explicit decision about which
  parameters are domain-referenced vs. just passed through.

- **[ForecastSeries.Mean allocates nothing but iterates Points]** For large
  forecast windows this is fine — Points typically has < 200 entries.

- **[HistoryAnalyzer API change]** The `ForecastHistoryActor` stores
  `ForecastHistory` in memory and via Akka persistence events. Changing the
  key type from string to `ParameterDef` affects serialised state. Since
  Akka persistence for this actor is append-only with retention cleanup, old
  events can be ignored or migrated via a recovery adapter.
