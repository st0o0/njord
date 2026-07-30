# daily-consensus-aggregation Specification

## Purpose

Server-side aggregation of hourly consensus medians into per-calendar-day summaries (temperature max/min, precipitation sum, wind speed max, weather code, spread, agreement, model count). Delivered via the `ConsensusUpdate` gRPC message so clients don't need to derive daily values from hourly horizons.

## Requirements

<!-- Requirement REMOVED: ConsensusResult includes daily summaries aggregated from hourly consensus medians
Reason: Redundant — daily consensus now comes directly from model daily parameters via the `DailyConsensus` facet of `ConsensusSnapshot`. The hourly->daily rollup produced inaccurate results (e.g., `max(hourly medians)` underestimates the true daily max) and was duplicated by ha-njord's own client-side aggregation.
Migration: Consumers that used `DailyConsensusSummary` fields (TemperatureMax, TemperatureMin, PrecipitationSum, WindSpeedMax, WeatherCode) SHALL use `DailyConsensus.Parameters` with the corresponding daily parameter definitions (`temperature_2m_max`, `temperature_2m_min`, `precipitation_sum`, `wind_speed_10m_max`, `weather_code`). -->

<!-- Requirement REMOVED: DailyConsensusSummary aggregation logic
Reason: Entire type removed — `DailyConsensusSummary` record and its `Aggregate` static method are deleted. All aggregation logic (GroupHorizonsByDay, CollectValues, FindNoonWeatherCode) is removed.
Migration: Use `DailyConsensus.Parameters` from `ConsensusSnapshot.Daily`. -->

<!-- Requirement REMOVED: DailyConsensus proto message
Reason: The `DailyConsensus` proto message type (which mapped `DailyConsensusSummary`) is removed from the gRPC API. The `ConsensusUpdate` proto message's `daily_summaries` field is deprecated.
Migration: gRPC clients SHALL use `ParameterConsensus` entries with `dN` horizon keys for daily data, same as hourly. -->

<!-- Requirement REMOVED: ConsensusUpdate carries daily summaries
Reason: `ConsensusUpdate.daily_summaries` repeated field is deprecated. Daily consensus is now represented as `ParameterConsensus` entries in the main `parameters` list (or a new `daily_parameters` repeated field) with `dN` horizon keys.
Migration: gRPC clients switch from `daily_summaries` to `daily_parameters` on the `ConsensusUpdate` message. -->
