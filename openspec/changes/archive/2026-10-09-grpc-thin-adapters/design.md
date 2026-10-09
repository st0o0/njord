## Context

`Njord.Grpc` is njord's only direct analogue to FunkArr's adapter layer
(`FunkArr.Api`/`FunkArr.ArrApi`). FunkArr keeps controllers at 20-70 lines and funnels all
request/response shaping through small (`12-50` line) pure extension-method mappers
(`RuleSetMappingExtensions`, `SabnzbdResponseMapper`). `WeatherGrpcService` (425 lines),
`EnrichmentProtoMapper` (496 lines), `OpsGrpcService` (199 lines), and `AdminGrpcService`
(359 lines) instead mix actor queries, business logic (coverage-tier lookup, budget
calculation), and large inline protobuf-construction blocks in the same methods.

## Goals / Non-Goals

**Goals:**
- Pull pure request->response shaping out of the gRPC service classes into small, named,
  unit-testable mapper functions/classes, following FunkArr's `*MappingExtensions`/
  `*ResponseMapper` naming.
- Eliminate the verified duplication between `OpsGrpcService.GetStatus` and `GetTargets`.
- Keep every RPC's wire behavior byte-identical for identical input/actor state.

**Non-Goals:**
- No `.proto` changes.
- No change to actor query patterns (`actorRegistry.GetAsync<T>().Ask<TResult>(...)`) — this
  change only touches what happens to the result after the `Ask` call returns.
- Not restructuring `GrpcSnapshotConsumerActor` or the snapshot actors themselves.

## Decisions

1. **`CatalogMapper` (new, static, `Njord.Grpc`)**: `ToModelInfo(string modelId)` and
   `ToLocationInfo(LocationOptions, IReadOnlyList<string> models)`, extracted verbatim from
   `WeatherGrpcService.GetCatalog` (current lines 33-85). `GetCatalog` becomes a
   location/model loop calling these two functions and deduplicating via the existing
   `HashSet<string> seenModels`.
   Alternative considered: keep `GetCatalog` as one method but shrink the `ModelInfo`
   construction via a helper lambda. Rejected — a named static mapper is independently
   testable without `ServerCallContext`/`ActorRegistry`, matching FunkArr's approach of
   testing mappers without spinning up the adapter.

2. **`WeatherGrpcService.MapForecastPoints` split into `ToHourlyForecast(ForecastPoint
   point)` and `ToDailyForecast(DailyForecastPoint point)`**, each returning a fully built
   proto message. The two callers (`MapForecastResponse`, `MapForecastUpdate` — these two
   already share `MapForecastPoints`, so no change needed there) become
   `forecast.Hourly.Points.Select(ToHourlyForecast)` / `.Select(ToDailyForecast)`.
   Note: verified during research that `MapForecastResponse`/`MapForecastUpdate` already
   delegate to one shared `MapForecastPoints` — the duplication claim for those two wrapper
   methods specifically was not confirmed; this decision only addresses the size/density of
   `MapForecastPoints` itself, not a duplication between the wrappers.

3. **`PollStatusMapper.ToModelStatus(PollStateEntry)` / `.ToTriggerTarget(PollStateEntry)`**:
   verified duplication — `OpsGrpcService.GetStatus` (lines 77-101) and `GetTargets` (lines
   152-171) build `ModelStatus` and `TriggerTarget` respectively from the same
   `QueryPollStatesResult.Entries`, with identical field-by-field logic (Location, Model,
   `Phase == PollPhase.Steady ? "steady" : "discovery"`, `NextPoll`, `MissCount`, conditional
   `LastChange`, conditional `CycleSeconds`). Extract one shared mapper per target type;
   both call sites become a single `.Select(entry => PollStatusMapper.ToXxx(entry))`.

4. **`AdminGrpcService`**: split `SetLocations`/`SetSettings` into named private steps
   (`MapRequest`, `Validate`, `Apply`) rather than one interleaved method body. No new
   public API.

5. **`EnrichmentProtoMapper`**: apply the same per-feature decomposition discussed in the
   sibling `mqtt-discovery-component-builder` change (separate, already-identified
   duplication pattern) — group the repeated inline construction blocks per enrichment
   feature behind small named functions.

## Risks / Trade-offs

- [Risk] Splitting large methods into several small ones could subtly change which
  `HashSet`/dedup state is shared across calls. → Mitigation: keep all cross-item state
  (`seenModels` in `GetCatalog`) in the calling loop, not inside the extracted pure
  functions — the functions take plain values in, return plain proto messages out, with no
  shared mutable state.
- [Risk] Golden-master/snapshot tests (if any exist for gRPC responses) could be sensitive
  to field-population order. → Mitigation: preserve the exact field-assignment order inside
  each extracted function; run `Njord.Grpc.Tests` before and after each extraction step.

## Migration Plan

Pure refactor, single deploy, no proto/wire changes, trivial revert.

## Open Questions

None.
