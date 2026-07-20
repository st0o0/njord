# daily-consensus Specification

## Purpose

Multi-model consensus aggregation for daily forecast parameters: computes Median, TrimmedMean, Spread, IQR, Agreement, Outlier, ConfidenceInterval, and AvailableModels per daily parameter per day-horizon (d0–dN). Extends the consensus device with daily components and state messages.

## Requirements

### Requirement: Daily consensus computes multi-model statistics per daily parameter per day-horizon
`ConsensusResult.Compute` SHALL iterate all resolved daily parameters from `ResolvedParameterSet.Daily` and, for each parameter, collect the value from each model's `DailyForecastSeries` at each day-offset (d0 = today, d1 = tomorrow, ..., dN). It SHALL compute the same statistics as hourly consensus (Median, TrimmedMean, Spread, IQR, Agreement, Outlier, ConfidenceInterval, AvailableModels) per (parameter, day-horizon) pair.

#### Scenario: Three models with daily temperature_2m_max
- **WHEN** 3 models report temperature_2m_max for d1 as [28.0, 31.0, 29.5]
- **THEN** the daily consensus for temperature_2m_max at d1 has Median=29.5, Spread=3.0, and AvailableModels contains all 3 models

#### Scenario: One model missing a daily value
- **WHEN** 3 models are configured but only 2 have a value for uv_index_max at d2 (one is null)
- **THEN** the consensus is computed from the 2 available values and AvailableModels contains only those 2

#### Scenario: Fewer than 2 models have a daily value
- **WHEN** only 1 model provides precipitation_sum for d3
- **THEN** that day-horizon is filtered out (not included in the result) by the minModels=2 filter

### Requirement: Daily consensus results are stored in a separate DailyParameters collection
`ConsensusResult` SHALL expose a `DailyParameters` property of type `IReadOnlyList<ParameterConsensus>` alongside the existing `Parameters` (hourly). The horizon keys in `ByHorizon` for daily entries SHALL use the format `d{N}` where N is the day offset from today (0-based).

#### Scenario: Result structure separates hourly and daily
- **WHEN** consensus is computed for a snapshot with both hourly and daily data
- **THEN** `result.Parameters` contains hourly entries keyed `h0`–`hN` and `result.DailyParameters` contains daily entries keyed `d0`–`dN`

### Requirement: Day offset is computed from UTC date of the cycle
The day-horizon d0 SHALL correspond to the UTC date of the poll cycle timestamp. d1 = d0 + 1 day, etc. The number of daily horizons SHALL be bounded by the number of days available in the shortest model's daily series that has ≥ 2 models reporting (analogous to the hourly cutoff logic).

#### Scenario: Cycle at 2026-07-19T14:00Z
- **WHEN** the cycle timestamp is 2026-07-19T14:00Z and models provide daily data for 2026-07-19 through 2026-07-22
- **THEN** d0 = 2026-07-19, d1 = 2026-07-20, d2 = 2026-07-21, d3 = 2026-07-22

### Requirement: Discovery emits daily consensus components on the consensus device
`ConsensusEnrichment.BuildDiscoveryPayload` SHALL emit one sensor component per (daily parameter, day-horizon) pair using the key format `{param.JsonKey}_d{N}`. The state topic SHALL use sub-topic `d{N}` (parallel to hourly `h{N}`). The component template extracts `value_json.{param.JsonKey}`.

#### Scenario: Discovery payload includes daily UV max
- **WHEN** the resolved daily parameters include `uv_index_max` and ForecastDays=4
- **THEN** the discovery payload contains components `uv_index_max_d0` through `uv_index_max_d3` with appropriate state topics

### Requirement: State messages emit one JSON per daily horizon
`StatePayloadBuilder.FromConsensus` SHALL emit one MQTT message per daily horizon (topic: `{baseTopic}/{location}/consensus/d{N}`) containing one JSON object with all daily parameter consensus values for that day.

#### Scenario: Daily state message content
- **WHEN** daily consensus produces results for temperature_2m_max (29.5) and precipitation_sum (12.3) at d1
- **THEN** the state message for topic `.../consensus/d1` contains `{"temperature_2m_max": 29.5, "precipitation_sum": 12.3}`
