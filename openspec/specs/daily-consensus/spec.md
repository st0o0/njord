# daily-consensus Specification

## Purpose

Multi-model consensus aggregation for daily forecast parameters: computes Median, TrimmedMean, Spread, IQR, Agreement, Outlier, ConfidenceInterval, and AvailableModels per daily parameter per day-horizon (d0–dN). Extends the consensus device with daily components and state messages.

## Requirements

### Requirement: Daily consensus computes multi-model statistics per daily parameter per day-horizon

Daily consensus SHALL be computed as part of `ConsensusSnapshot.Compute`, producing the `DailyConsensus` facet. It uses daily model parameters (`temperature_2m_max`, `temperature_2m_min`, `precipitation_sum`, `weather_code`, etc.) directly from `DailyForecastSeries`. It SHALL NOT use hourly->daily rollups.

#### Scenario: Three models with daily temperature_2m_max
- **WHEN** 3 models provide `temperature_2m_max` values of 24.0, 25.5, 26.0 for d0
- **THEN** the daily consensus for `temperature_2m_max` at d0 SHALL have median 25.5

#### Scenario: One model missing a daily value
- **WHEN** 2 of 3 models provide a daily value
- **THEN** the consensus is computed from the 2 available values

#### Scenario: Fewer than 2 models have a daily value
- **WHEN** only 1 model provides a daily value for d3
- **THEN** d3 SHALL be excluded from the daily consensus (filtered by min-2-models rule)

### Requirement: Daily consensus results are stored in the DailyConsensus facet

The `DailyConsensus` facet of `ConsensusSnapshot` SHALL contain `Parameters` (list of `ParameterConsensus` with `dN` horizon keys) and `CutoffDay`.

#### Scenario: Result structure separates hourly and daily
- **WHEN** `ConsensusSnapshot.Compute` produces a result
- **THEN** `Hourly.Parameters` contains hourly data with `hN` keys and `Daily.Parameters` contains daily data with `dN` keys

### Requirement: Day offset is computed from UTC date of the cycle

The system SHALL compute d0 as the UTC date of the consensus cycle timestamp. Subsequent days SHALL increment by one calendar day.

#### Scenario: Cycle at 2026-07-19T14:00Z
- **WHEN** the consensus cycle runs at 2026-07-19T14:00Z
- **THEN** d0 corresponds to 2026-07-19, d1 to 2026-07-20, etc.

### Requirement: Discovery emits daily consensus components on the consensus device

The consensus device discovery payload SHALL include one sensor component per (daily parameter, day offset) pair.

#### Scenario: Discovery payload includes daily UV max
- **WHEN** discovery runs and `uv_index_max` is a daily parameter
- **THEN** the consensus device includes components `uv_index_max_d0`, `uv_index_max_d1`, etc.

### Requirement: State messages emit one JSON per daily horizon

The consensus egress SHALL publish one retained MQTT message per daily horizon to `{baseTopic}/{location}/consensus/d{N}`.

#### Scenario: Daily state message content
- **WHEN** consensus egress processes `Daily.Parameters`
- **THEN** one retained MQTT message per day horizon is published to `{baseTopic}/{location}/consensus/d{N}` with parameter medians and model counts
