# Wire Pipeline Analysis Output

## Problem

The `restructure-enrichment-pipeline` change created vertical feature libraries
and a `BroadcastHub<PipelineEvent>` output, but left two gaps that break E2E
functionality:

1. **No analysis results in the pipeline output.** `PipelineActor` maps
   `FetchOutcome.Success` → `ModelForecastUpdate` only. The consensus →
   feature fan-out → merge step was deferred as transitional. No
   `AnalysisResult` events are emitted, so enrichment entities in HA show
   unavailable, gRPC `GetEnrichments` returns empty, and `weather.consensus` /
   `binary_sensor.inversion` are never created.

2. **`GetForecast` unary RPC broken.** The deleted `ModelStateActor` used to
   forward `UpdateForecast` to the `ForecastSnapshotActor` shard region.
   Nobody sends that message anymore. `StreamForecasts` works (reads from the
   BroadcastHub), but the unary `GetForecast` query finds an empty shard.

## Approach

### Analysis graph via injected stage

`PipelineActor` lives in `Njord.Pipeline`, which must not reference feature
libraries. Define an `IAnalysisStage` interface in `Njord.Core` that the Host
implements. `PipelineActor` receives it via DI and plugs it into the stream
graph between `FetchOutcome.Success` and the `BroadcastHub<PipelineEvent>`.

The Host implementation (`AnalysisStage`) wires `ConsensusSnapshotFactory`,
`Scan` (for previous consensus / ModelSnapshot accumulation), `PollContext`
construction, and all five feature analyzers (alerts, derived, trends, indices,
history). Stateless features run via `Select`; history runs via `SelectAsync`
with an Ask to `ForecastHistoryActor`.

### Eliminate `ForecastSnapshotActor` shard

`GrpcConsumerActor` already holds the latest `ModelForecastUpdate` per location
in memory. Change `GetForecast` in `WeatherGrpcService` to query
`GrpcConsumerActor` instead of the snapshot shard. Remove
`ForecastSnapshotActor`, its shard registration, its persistence DTOs, and the
`IForecastSnapshotRegion` marker interface.

Trade-off: after a restart, `GetForecast` returns "not available" until the
first poll completes (~seconds to minutes). This matches `GetEnrichments`
behavior and avoids the persistence overhead for transient forecast data.

## Scope

- Wire the full analysis pipeline in `PipelineActor` via `IAnalysisStage`
- Implement `AnalysisStage` in the Host with consensus + 5 feature analyzers
- Emit `PipelineEvent.AnalysisResult` for each feature result
- Emit `PipelineEvent.ModelCapability` from the fetch stage
- Remove `ForecastSnapshotActor` shard, route `GetForecast` through
  `GrpcConsumerActor`
- Update `GrpcConsumerActor.LatestResults` to hold per-model forecasts
- Fix `MqttStateActor` TODO: map `AnalysisResult` to enrichment state payloads
- Update tests and architecture rules

## Non-goals

- Typed `AnalysisResult.Result` — stays as `object` for now; typed extraction
  happens in consumers (gRPC proto mapper, MQTT state builder)
- KeepAlive + Buffer + IdleTimeout on the pipeline BroadcastHub — deferred
  until consumer stability issues are observed
- MQTT Discovery re-introduction — out of scope, remains removed
- `PollContext` construction from multiple locations — single-location only
  for now (matches config)

## API-budget impact

No change. The pipeline fetches the same models at the same intervals. Analysis
is pure compute on already-fetched data.
