## Why

A cross-codebase review against the sibling FunkArr project found that `Njord.Grpc`'s
service implementations are not thin adapters: they mix catalog/coverage business logic,
duplicated response-building, and large inline protobuf construction directly into the gRPC
service classes. FunkArr's equivalent adapter layer (`FunkArr.ArrApi` controllers +
`RuleSetMappingExtensions`) keeps controllers at 20-70 lines, delegating all mapping to
small (12-50 line) pure extension-method mappers. Bringing `Njord.Grpc` to the same shape
makes the mapping logic independently unit-testable and shrinks the two largest files
(`WeatherGrpcService.cs`, 425 lines; `EnrichmentProtoMapper.cs`, 496 lines) by extracting
what doesn't need `ServerCallContext`/actor access into pure functions.

## What Changes

- `WeatherGrpcService.GetCatalog` (lines 31-88): extract the per-model `ModelInfo` /
  `CoverageTier` construction (lines 54-82) into a pure `CatalogMapper.ToModelInfo(string
  modelId)` function, and the per-location `LocationInfo` construction into
  `CatalogMapper.ToLocationInfo(LocationOptions, IReadOnlyList<string> models)`. The service
  method becomes a loop over locations/models calling these two functions.
- `WeatherGrpcService.MapForecastPoints` (lines 303-415, ~110 lines): split the per-field
  `SetOptional` sequence into two smaller pure functions, `ToHourlyForecast(ForecastPoint)`
  and `ToDailyForecast(DailyForecastPoint)`, so each field-mapping concern is independently
  testable and the main method is a single `.Select(...)` per series instead of a long
  imperative block.
- `OpsGrpcService.GetStatus` and `GetTargets` (lines 39-139 and 146-180): both build a
  near-identical shape (Location, Model, Phase ternary, NextPoll, MissCount, optional
  LastChange, optional CycleSeconds) from the same `QueryPollStatesResult.Entries`. Extract a
  single `PollStatusMapper.ToModelStatus(PollStateEntry)` / `.ToTriggerTarget(PollStateEntry)`
  pair (or one shared intermediate record consumed by both), removing the duplicated
  conditional-assignment block.
- `AdminGrpcService.SetLocations`/`SetSettings` (lines ~42-124): separate request-mapping,
  validation, and budget-recalculation into named private steps (or a small
  `AdminRequestMapper`), so the RPC method reads as "map -> validate -> apply -> respond"
  instead of interleaving all three.
- `EnrichmentProtoMapper.cs`: apply the same "many near-identical inline constructions"
  decomposition used in the MQTT-discovery change (separate proposal) — group repeated
  per-feature proto-building blocks behind small per-feature mapper functions.

## Capabilities

### New Capabilities

- `grpc-mapping-layer`: the implementation-pattern requirement (mirroring the existing
  `actor-state-pattern` capability's style) that gRPC service classes delegate
  request/response shaping to named, pure mapper functions rather than inlining it in RPC
  methods.

### Modified Capabilities

None. `GetCatalogResponse`, `GetForecastResponse`, `StatusResponse`, `GetTargetsResponse`,
and all enrichment proto payloads must remain byte-for-byte identical for the same input;
the existing `grpc-v2-weather-service`, `grpc-v2-ops-service`, `grpc-v2-admin-service`, and
`grpc-enrichment-api` specs are unaffected because none of them specify implementation
structure, only wire behavior.

## Impact

- `src/Njord.Grpc/WeatherGrpcService.cs`, new `src/Njord.Grpc/CatalogMapper.cs`
- `src/Njord.Grpc/OpsGrpcService.cs`, new `src/Njord.Grpc/PollStatusMapper.cs`
- `src/Njord.Grpc/AdminGrpcService.cs`
- `src/Njord.Grpc/EnrichmentProtoMapper.cs`
- Matching tests: `src/Njord.Grpc.Tests/`
- No `.proto` changes, no wire-format changes.

## Non-goals

- No change to any `.proto` definition or RPC signature.
- No change to response field values for any existing request — this is purely moving
  existing logic into named, independently testable functions.
- Not addressing `Njord.Mqtt`'s `DiscoveryPayloadBuilder` duplication (separate change).

## API budget impact

None. This change touches only the gRPC-facing API layer; it does not affect Open-Meteo
polling behavior or request volume.
