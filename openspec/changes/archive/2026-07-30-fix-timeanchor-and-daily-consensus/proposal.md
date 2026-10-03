## Why

`TimeAnchor.AtHorizon` rounds to the **ceiling** (next whole hour) instead of the floor. Open-Meteo hourly data points describe the current hour interval (e.g., the `14:00` data point covers 14:00–14:59), so at 14:25 the correct anchor for h0 is `14:00`, not `15:00`. This causes two observable bugs:

1. **Consensus current temperature higher than all individual models.** Live observation: consensus showed 24.9 °C while every model reported 22.2–23.9 °C. The ceiling shifts h0 one hour into the future; when temperatures are rising, the consensus reads a warmer forecast point than what "now" actually is.
2. **Daily summary day-boundary shift.** Because every horizon is shifted +1 h by the ceiling, `DailyConsensusSummary.GroupHorizonsByDay` assigns early-morning hours to the wrong calendar day, causing daily high/low values to reset at the wrong time (~06:00 local instead of midnight).

A secondary inconsistency exists between `HorizonProjection` (exact dictionary lookup by `ValidAt`) and `ConsensusResult.ComputeHourly` (±30-minute fuzzy search). Once the anchor is correct (floor), the exact lookup is the right approach and the fuzzy search in `ComputeHourly` should be tightened to match.

## What Changes

- **`TimeAnchor.AtHorizon`**: Change from ceiling to floor rounding. The anchored time for horizon h becomes `floor(now + h hours)` — truncate to the current whole hour, never round up.
- **`ConsensusResult.ComputeHourly`**: Replace the ±30-minute fuzzy `FirstOrDefault` point lookup with an exact `ValidAt` match (same approach as `HorizonProjection`), now that the anchor targets the correct hour.
- **Tests**: Update all `TimeAnchor` and consensus tests to reflect floor semantics. Add a regression test verifying that h0 consensus median falls within the range of individual model values for that hour.

## Non-goals

- Changing the `DailyConsensusSummary` aggregation strategy (deriving daily stats from hourly medians vs. using daily API parameters). The existing spec defines the hourly-derived approach; the ceiling bug was the root cause of the day-boundary problem.
- Changing poll interval, request budget, or any API call patterns — no polling changes involved.

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

- `consensus-computation`: The `TimeAnchor` floor-rounding semantic changes which forecast data point each horizon resolves to, and the point-lookup strategy in `ComputeHourly` changes from fuzzy to exact.
- `daily-consensus-aggregation`: The day-boundary grouping implicitly changes because `TimeAnchor` now floors instead of ceils — horizons land in the correct calendar day.

## Impact

- **Domain**: `TimeAnchor.cs`, `ConsensusResult.cs` (ComputeHourly point lookup)
- **Egress**: `HorizonProjection.cs` — no code change needed, already uses floor+exact lookup; just needs the same `TimeAnchor` fix
- **Tests**: `TimeAnchorSpec`, `ConsensusResultSpec`, `DailyConsensusSummarySpec`, `HorizonProjectionSpec` — all tests that construct expectations based on ceiling semantics
- **No API budget impact** — no change to polling frequency or request shape
