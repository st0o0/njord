## Context

The consensus enrichment already computes per-hour, per-parameter medians across all weather models and publishes them via MQTT (hourly horizons h0–hN) and gRPC (`ConsensusUpdate.parameters`). The existing daily consensus (multi-model median of Open-Meteo's `temperature_2m_max` etc.) is published via MQTT but not surfaced in gRPC.

The ha-njord frontend receives hourly consensus via gRPC and tries to derive daily summaries client-side. This fails in the afternoon because past horizons are filtered out, losing the warm midday hours. The server must provide pre-aggregated daily summaries that cover the full calendar day.

## Goals / Non-Goals

**Goals:**
- Aggregate hourly consensus medians into per-calendar-day summaries server-side.
- Deliver these via the existing `ConsensusUpdate` proto message (additive, wire-compatible).
- Correctly bucket hours into calendar days using the location's timezone.

**Non-Goals:**
- Replacing the existing MQTT daily consensus (it serves a different purpose: multi-model median of Open-Meteo daily parameters).
- Client-side fixes in ha-njord (the server fix makes the client computation unnecessary).
- Timezone auto-detection from lat/lon (requires a geo-tz library; not worth the dependency).

## Decisions

### 1. Aggregate from hourly consensus, not from existing daily consensus

The hourly consensus `temperature_2m` medians are the values the client already sees. Taking max/min across a full calendar day's hours gives the daily high/low. This is semantically what the client was trying to compute, just done correctly on the server.

Alternative: Surface the existing `DailyParameters` (multi-model median of Open-Meteo daily max/min) via gRPC. Rejected because those values may diverge from what the hourly consensus shows — the client would see inconsistent data between hourly and daily views.

### 2. Add `Timezone` to `LocationOptions` configuration

Calendar-day bucketing requires knowing the location's timezone. Open-Meteo returns daily data aligned to the location's timezone, but njord uses `timeformat=unixtime` and doesn't capture it.

Add an optional `Timezone` property (IANA id, e.g. `"Europe/Zurich"`) to `LocationOptions`. Default to UTC when unset. This keeps the config explicit and avoids a geo-tz NuGet dependency.

Alternative: Derive timezone from lat/lon using a library like GeoTimeZone. Rejected — adds a dependency for one feature, and most deployments have 1-3 fixed locations where specifying the timezone is trivial.

### 3. Domain type `DailyConsensusSummary` as a plain record

New sealed record in `Njord.Domain.Analysis`:

```
DailyConsensusSummary(
    DateOnly Date,
    double? TemperatureMax,
    double? TemperatureMin,
    double? PrecipitationSum,
    double? WindSpeedMax,
    int? WeatherCode,
    double? Spread,
    double? Agreement,
    int AvailableModels)
```

Computed by a static method `DailyConsensusSummary.Aggregate(ConsensusResult, DateTimeOffset now, TimeZoneInfo tz)` that:
1. Groups hourly horizons by calendar day in the given timezone.
2. For each day, scans the relevant parameters' `HorizonConsensus` entries.
3. Computes the aggregates per the requirement spec.

### 4. Aggregation lives in `ConsensusEnrichment.Compute`, not in the mapper

The mapper is a thin proto conversion layer. The aggregation is domain logic that should run once when the consensus result is produced, not re-computed on every gRPC request. `ConsensusEnrichment.Compute` already has access to the `ConsensusResult`, the `TimeProvider`, and (via options) the location config including the new timezone.

The `ConsensusResult` record gains a `DailySummaries` property: `IReadOnlyList<DailyConsensusSummary>`. The enrichment populates it after computing the hourly/daily consensus. The mapper then trivially maps it to proto.

### 5. Proto message `DailyConsensus`

```protobuf
message DailyConsensus {
  string date = 1;
  optional double temperature_max = 2;
  optional double temperature_min = 3;
  optional double precipitation_sum = 4;
  optional double wind_speed_max = 5;
  optional int32 weather_code = 6;
  optional double spread = 7;
  optional double agreement = 8;
  int32 available_models = 9;
}
```

Added to the "Enrichment: Consensus" section of `common.proto`. `ConsensusUpdate` gains field 2: `repeated DailyConsensus daily = 2`.

### 6. Parameter lookup for aggregation

The aggregation needs to find the right `ParameterConsensus` entries by parameter name. The hourly consensus already has `temperature_2m`, `precipitation`, `wind_speed_10m`, and `weather_code` as hourly parameters. The aggregation looks up these by `ParameterDef.ApiName` in `ConsensusResult.Parameters`.

## Risks / Trade-offs

- **[Timezone config burden]** → Users must add `Timezone` to their location config. Mitigated: UTC default works for most use cases; only matters when the location is far from UTC and the user cares about accurate calendar-day boundaries.
- **[Hourly-aggregated daily != Open-Meteo daily]** → The daily high from hourly medians may differ slightly from the median of Open-Meteo's daily max values. This is acceptable because the hourly aggregation matches what the client displays for hourly data, ensuring UI consistency.
- **[Weather code at noon]** → Using the horizon closest to local noon is a heuristic. A day with morning rain and afternoon sun would show the noon weather code, not the "dominant" one. Acceptable for a summary view.
