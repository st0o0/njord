# daily-consensus-aggregation Specification

## Purpose

Server-side aggregation of hourly consensus medians into per-calendar-day summaries (temperature max/min, precipitation sum, wind speed max, weather code, spread, agreement, model count). Delivered via the `ConsensusUpdate` gRPC message so clients don't need to derive daily values from hourly horizons.

## Requirements

### Requirement: ConsensusResult includes daily summaries aggregated from hourly consensus medians
`ConsensusResult` SHALL expose a `DailySummaries` property of type `IReadOnlyList<DailyConsensusSummary>`. Each entry represents one calendar day and is computed by grouping hourly consensus horizons into calendar days using the timezone carried on the `ModelForecast` data for that location. The grouping SHALL use floor-anchored times from `TimeAnchor.AtHorizon` (truncated to the start of the hour, not rounded up).

#### Scenario: Full day with 24 hourly medians
- **WHEN** hourly consensus has medians for temperature_2m at h0–h23 covering a single calendar day in timezone Europe/Zurich, with values [18, 19, 20, 22, 24, 26, 28, 30, 31, 32, 33, 33, 32, 31, 30, 28, 26, 24, 22, 20, 19, 18, 17, 16]
- **THEN** `DailySummaries[0].TemperatureMax` = 33 and `DailySummaries[0].TemperatureMin` = 16

#### Scenario: Partial day still uses all available hours
- **WHEN** the consensus was computed at 14:00 UTC and the current calendar day in the location's timezone has hourly medians from h0 (covering the morning hours) through h10 (covering up to midnight local)
- **THEN** `DailySummaries[0]` SHALL include all hours of the calendar day, including hours before the consensus computation time

#### Scenario: No forecasts available defaults to UTC
- **WHEN** no `ModelForecast` entries exist for the location in the current snapshot
- **THEN** calendar-day bucketing SHALL use UTC

#### Scenario: Day boundary aligns with local midnight
- **WHEN** the consensus is computed at 22:30 UTC (00:30 CEST) with floor-anchored h0 = 22:00 UTC (00:00 CEST)
- **THEN** h0 SHALL be grouped into the new calendar day (the day starting at 00:00 CEST), not the previous day

### Requirement: DailyConsensusSummary aggregation logic
`DailyConsensusSummary` SHALL be a sealed record with fields: `Date` (DateOnly), `TemperatureMax` (double?), `TemperatureMin` (double?), `PrecipitationSum` (double?), `WindSpeedMax` (double?), `WeatherCode` (int?), `Spread` (double?), `Agreement` (double?), `AvailableModels` (int).

#### Scenario: Temperature max/min from hourly medians
- **WHEN** hourly consensus temperature_2m medians for a calendar day are [20.0, 25.0, 30.0, 28.0, 22.0]
- **THEN** `TemperatureMax` = 30.0 and `TemperatureMin` = 20.0

#### Scenario: Precipitation sum across hours
- **WHEN** hourly consensus precipitation medians for a calendar day are [0.0, 0.5, 1.2, 0.3, 0.0]
- **THEN** `PrecipitationSum` = 2.0

#### Scenario: Wind speed max from hourly medians
- **WHEN** hourly consensus wind_speed_10m medians for a calendar day are [3.0, 5.0, 8.0, 6.0, 4.0]
- **THEN** `WindSpeedMax` = 8.0

#### Scenario: Weather code at local noon
- **WHEN** hourly consensus weather_code medians exist at h3 (10:00 local), h4 (11:00 local), h5 (12:00 local), h6 (13:00 local) with values [3, 61, 80, 61]
- **THEN** `WeatherCode` = 80 (the value at the horizon closest to 12:00 local, rounded to int)

#### Scenario: Spread is average of temperature spread values
- **WHEN** hourly consensus temperature_2m spread values for a calendar day are [2.0, 3.0, 4.0, 3.0, 2.0]
- **THEN** `Spread` = 2.8

#### Scenario: Agreement is average of temperature agreement values
- **WHEN** hourly consensus temperature_2m agreement values for a calendar day are [0.8, 0.9, 0.7, 0.85, 0.9]
- **THEN** `Agreement` = 0.83

#### Scenario: Available models is minimum across hours
- **WHEN** hourly consensus available_models counts for a calendar day are [8, 7, 6, 7, 8]
- **THEN** `AvailableModels` = 6

#### Scenario: Missing parameter yields null
- **WHEN** no hourly consensus entries exist for precipitation on a given calendar day
- **THEN** `PrecipitationSum` = null

### Requirement: DailyConsensus proto message
`common.proto` SHALL define a `DailyConsensus` message in the "Enrichment: Consensus" section with fields: date (string, field 1), temperature_max (optional double, 2), temperature_min (optional double, 3), precipitation_sum (optional double, 4), wind_speed_max (optional double, 5), weather_code (optional int32, 6), spread (optional double, 7), agreement (optional double, 8), available_models (int32, 9).

#### Scenario: Proto message structure
- **WHEN** a `DailyConsensus` message is serialized with date="2026-07-29", temperature_max=33.0, temperature_min=16.0, available_models=8
- **THEN** the message SHALL contain all three fields with their values and optional fields unset SHALL be absent on the wire

### Requirement: ConsensusUpdate carries daily summaries
`ConsensusUpdate` SHALL include `repeated DailyConsensus daily = 2` alongside the existing `repeated ParameterConsensus parameters = 1`.

#### Scenario: Existing clients unaffected
- **WHEN** an existing client deserializes a `ConsensusUpdate` containing the new `daily` field
- **THEN** the unknown field SHALL be silently ignored (proto3 wire compatibility)

#### Scenario: New client receives daily data
- **WHEN** a new client calls `GetEnrichments` or subscribes to `StreamEnrichments`
- **THEN** the `ConsensusUpdate` SHALL contain `daily` entries for each calendar day covered by the hourly consensus horizons
