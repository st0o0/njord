## Why

The consensus weather entity in ha-njord computes daily forecasts client-side from hourly consensus horizons. The client filters out past horizons, so in the afternoon the warm midday hours are missing — the daily high drops below the current temperature. Example: Consensus shows 24.4 / 22.2 °C (High/Low) while all individual models show 32-34 / 16-19 °C. Individual models don't have this problem because their `DailyForecast` arrives pre-computed from the server covering the full calendar day.

## What Changes

- New proto message `DailyConsensus` in `common.proto` with temperature_max, temperature_min, precipitation_sum, wind_speed_max, weather_code, plus consensus metadata (spread, agreement, available_models).
- `ConsensusUpdate` gains `repeated DailyConsensus daily = 2`.
- New domain type `DailyConsensusSummary` aggregated server-side from hourly consensus medians grouped by calendar day (location timezone).
- `LocationOptions` gains an optional `Timezone` property (IANA, e.g. "Europe/Zurich") for calendar-day bucketing.
- `EnrichmentProtoMapper.MapConsensus` maps the new daily summaries.
- No changes to `weather.proto` — the `EnrichmentEvent.consensus` oneof already carries `ConsensusUpdate`.

## Non-goals

- Replacing the existing daily consensus (multi-model median of Open-Meteo daily parameters). That remains for MQTT and serves a different purpose.
- Changing the alert trigger_value display (Frost Alert 20.6 °C etc.) — that's a client UX concern.
- Changing polling frequency or request budget (no API-budget impact — this is pure server-side post-processing of data already fetched).

## Capabilities

### New Capabilities

- `daily-consensus-aggregation`: Server-side aggregation of hourly consensus medians into per-calendar-day summaries (temp max/min, precip sum, wind max, weather code, spread, agreement, model count) delivered via the `ConsensusUpdate` gRPC message.

### Modified Capabilities

- `grpc-enrichment-api`: `ConsensusUpdate` gains the `daily` field; `EnrichmentProtoMapper.MapConsensus` maps it.

## Impact

- **Proto**: `common.proto` — new `DailyConsensus` message, `ConsensusUpdate` field 2.
- **Domain**: New `DailyConsensusSummary` record and aggregation logic in the consensus enrichment pipeline.
- **Config**: `LocationOptions` gains optional `Timezone` (default derived from coordinates or UTC).
- **gRPC mapper**: `EnrichmentProtoMapper.MapConsensus` extended.
- **Tests**: New unit tests for the aggregation logic and mapper.
- **Wire compatibility**: Additive proto change — existing clients ignore the new field.
