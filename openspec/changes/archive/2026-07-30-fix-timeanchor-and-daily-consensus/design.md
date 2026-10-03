## Context

`TimeAnchor.AtHorizon(tick, hours)` is the single function that maps a poll timestamp + horizon offset to the forecast data point's `ValidAt` time. It is used by:

1. **`ConsensusResult.ComputeHourly`** — finds each model's data point for a given horizon, feeds values into the median/spread computation.
2. **`HorizonProjection.BuildPerHorizon`** — builds per-model MQTT state payloads (dictionary lookup by `ValidAt`).
3. **`DailyConsensusSummary.GroupHorizonsByDay`** — converts horizon keys to local timestamps for calendar-day bucketing.

Currently: `AtHorizon` floors the time then adds 1 hour (ceiling). Open-Meteo hourly data points use interval-start semantics (the `14:00` point describes 14:00–14:59), so the correct mapping is floor, not ceiling.

The point-lookup strategies also diverge: `HorizonProjection` does an exact `Dictionary<DateTimeOffset, point>.TryGetValue(anchor)`, while `ConsensusResult.ComputeHourly` does a linear scan with ±30-minute tolerance. Once the anchor is correct, both should use the same strategy.

## Goals / Non-Goals

**Goals:**
- h0 resolves to the current hour's data point, not the next hour's
- Consensus and per-model payloads use the same anchor and lookup strategy
- Daily summary day-boundaries align with local midnight

**Non-Goals:**
- Changing the daily summary aggregation approach (hourly-derived → daily API params)
- Adding interpolation between hourly data points
- Changing which horizons are computed (h0..cutoff stays the same)

## Decisions

### Decision 1: Floor rounding in TimeAnchor

**Change:** `AtHorizon` returns the floored hour unconditionally.

```csharp
// Before
var floored = new DateTimeOffset(..., target.Hour, 0, 0, target.Offset);
return floored == target ? target : floored.AddHours(1);

// After
return new DateTimeOffset(target.Year, target.Month, target.Day,
    target.Hour, 0, 0, target.Offset);
```

**Why floor over nearest-hour:** Open-Meteo data points use interval-start semantics. At 14:25 the governing data point is 14:00, not 15:00. Nearest-hour would flip at 14:30 and still produce incorrect results for the first half of each hour.

### Decision 2: Exact point lookup in ConsensusResult.ComputeHourly

**Change:** Replace the `FirstOrDefault` with ±30-min tolerance with an exact match, using a pre-built dictionary per forecast (same pattern as `HorizonProjection`).

```csharp
// Before (linear scan, fuzzy)
var point = forecast.Hourly.Points.FirstOrDefault(p =>
    Math.Abs((p.ValidAt - targetTime).TotalMinutes) < 30);

// After (dictionary, exact)
var pointsByValidAt = forecast.Hourly.Points.ToDictionary(p => p.ValidAt);
pointsByValidAt.TryGetValue(targetTime, out var point);
```

**Why exact over fuzzy:** With a correct floor anchor, the target time is always on the hour — exactly matching Open-Meteo's `ValidAt` timestamps. The fuzzy search was a workaround for the ceiling shift; with floor rounding it's no longer needed. Exact match is also O(1) per lookup instead of O(n).

**Why build the dictionary per forecast (outside the horizon loop):** Each forecast's points are iterated once to build the dictionary, then all horizon lookups are O(1). This is a minor perf improvement for high horizon counts (h0–h240+).

### Decision 3: No change to DailyConsensusSummary logic

The daily summary code in `GroupHorizonsByDay` calls `TimeAnchor.AtHorizon` and converts to local time. With the floor fix, horizons land in the correct calendar day automatically. No logic change needed in `DailyConsensusSummary` itself — the fix is inherited from `TimeAnchor`.

## Risks / Trade-offs

**[Risk] Existing tests encode ceiling expectations** → Audit all tests that construct expected `DateTimeOffset` values from `TimeAnchor`. Each ceiling-based expectation needs to shift back by 1 hour. Missed tests will fail obviously (wrong hour), so risk of silent regression is low.

**[Risk] 3-hourly models (e.g., ECMWF) with non-aligned timestamps** → ECMWF provides data at h0, h3, h6... With exact lookup, a floor-anchored target at e.g. h1 (01:00) won't find a data point — `TryGetValue` returns false, model contributes `null` for that horizon. This is the correct behavior: ECMWF doesn't have h1 data. The fuzzy search previously could match the h0 point (within 30 min) for h1, which was technically wrong — it attributed the h0 value to h1.

**[Trade-off] Dictionary allocation per forecast per poll** → One `Dictionary<DateTimeOffset, HourlyDataPoint>` per model per poll cycle. With ~10 models × ~100 points each = ~1000 entries total. Negligible memory cost, offset by O(1) lookups replacing O(n) scans.
