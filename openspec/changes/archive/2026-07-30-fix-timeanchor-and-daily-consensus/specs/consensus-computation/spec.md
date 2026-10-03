## ADDED Requirements

### Requirement: TimeAnchor uses floor rounding for horizon-to-timestamp mapping
`TimeAnchor.AtHorizon(tick, horizonHours)` SHALL compute the target time as `tick + horizonHours` and then truncate to the start of that hour (floor). It SHALL NOT round up to the next hour. Open-Meteo hourly data points use interval-start semantics: the data point at 14:00 describes the interval 14:00–14:59.

#### Scenario: Mid-hour tick floors to current hour for h0
- **WHEN** tick is 2026-07-30T14:25:00Z and horizonHours is 0
- **THEN** the result SHALL be 2026-07-30T14:00:00Z

#### Scenario: Exact hour tick stays unchanged
- **WHEN** tick is 2026-07-30T14:00:00Z and horizonHours is 0
- **THEN** the result SHALL be 2026-07-30T14:00:00Z

#### Scenario: Non-zero horizon floors the offset hour
- **WHEN** tick is 2026-07-30T14:25:00Z and horizonHours is 3
- **THEN** the result SHALL be 2026-07-30T17:00:00Z

#### Scenario: Floor never produces a future hour for h0
- **WHEN** tick is any time within the hour 14:00–14:59
- **THEN** AtHorizon(tick, 0) SHALL always return 14:00:00Z — never 15:00:00Z

### Requirement: Hourly consensus uses exact point lookup by ValidAt
`ConsensusResult.ComputeHourly` SHALL look up each model's forecast data point using an exact match on `ValidAt` (via a dictionary keyed by `DateTimeOffset`). It SHALL NOT use a fuzzy ±30-minute tolerance window. Models whose forecast series does not contain a data point at the exact anchored time SHALL contribute `null` for that horizon.

#### Scenario: Model with hourly resolution matches exactly
- **WHEN** icon_d2 has a data point at ValidAt = 2026-07-30T14:00:00Z and the anchored time for h0 is 2026-07-30T14:00:00Z
- **THEN** icon_d2's value SHALL be included in the consensus computation for h0

#### Scenario: 3-hourly model has no data at non-aligned hour
- **WHEN** ecmwf_ifs025 has data points at h0, h3, h6... and the anchored time for h1 is 2026-07-30T15:00:00Z
- **THEN** ecmwf_ifs025 SHALL contribute `null` for h1 (no data point at 15:00:00Z)

#### Scenario: Dictionary is built once per forecast for efficiency
- **WHEN** computing consensus across h0–h48 for a model with 49 data points
- **THEN** the implementation SHALL build a single dictionary per forecast and perform O(1) lookups per horizon (not a linear scan per horizon)

## MODIFIED Requirements

### Requirement: ConsensusResult holds both hourly and daily parameter consensus
`ConsensusResult` SHALL be a record with two collections: `Parameters` (hourly, existing) and `DailyParameters` (daily, new). Both are `IReadOnlyList<ParameterConsensus>`. The `Compute` static method SHALL accept the full `ResolvedParameterSet` and iterate both `.Hourly` and `.Daily` lists, producing separate collections in the result. The hourly computation SHALL use exact `ValidAt` lookup (not fuzzy ±30-minute tolerance) after computing the floor-anchored target time via `TimeAnchor.AtHorizon`.

#### Scenario: Compute produces both hourly and daily results
- **WHEN** `ConsensusResult.Compute` is called with a ResolvedParameterSet containing 30 hourly and 16 daily parameters
- **THEN** the result contains up to 30 entries in `Parameters` and up to 16 entries in `DailyParameters`

#### Scenario: Empty daily parameter set
- **WHEN** the ResolvedParameterSet has an empty `Daily` list
- **THEN** `DailyParameters` is an empty list (no error)

#### Scenario: Hourly point lookup uses exact match
- **WHEN** computing hourly consensus for temperature_2m at h0 with tick at 14:25 UTC
- **THEN** the implementation SHALL look for data points with ValidAt = 14:00 UTC (floor-anchored), not 15:00 UTC (ceiling), and SHALL use an exact dictionary lookup (not ±30-minute fuzzy search)
