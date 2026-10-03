## Why

The gRPC API has grown organically across two services (ForecastService, ConfigService)
with inconsistent time representations (int64, ISO string, Timestamp), overlapping model
queries (GetLocations + GetModels vs GetStatus vs GetTriggerTargets), and a ConfigService
that mixes read, CRUD, settings, and operations concerns into 11 RPCs. A clean-sheet
redesign splits the API into three client-aligned services with unified types.

## What Changes

- **BREAKING**: Delete `protos/njord/v1/` and both v1 service implementations.
- **BREAKING**: Replace with `protos/njord/v2/` containing four proto files:
  - `common.proto` — shared types (LocationInfo, ModelInfo, CoverageTier,
    ParameterValue, HourlyForecast, DailyForecast, all enrichment payloads).
    All temporal fields use `google.protobuf.Timestamp`.
  - `weather.proto` — `WeatherService` for read/stream (GetCatalog, GetForecast,
    GetEnrichments, StreamForecasts, StreamEnrichments).
  - `admin.proto` — `AdminService` for configuration (GetConfig, StreamConfig,
    SetLocations, SetSettings, SetEnrichment, SetBudget).
  - `ops.proto` — `OpsService` for operations (GetStatus, GetTargets, TriggerPoll).
- Merge `GetLocations` + `GetModels` into single `GetCatalog` RPC.
- Replace Location CRUD (Add/Remove/Update) with deklarative `SetLocations` (replace-all).
- Merge `UpdateForecastSettings` fields + `default_models` into `SetSettings`.
- Unify all time fields to `google.protobuf.Timestamp` (except `DailyForecast.date`
  which stays `string` for date-only values).
- Enrichment payload messages (AlertUpdate, IndexUpdate, etc.) move 1:1 to `common.proto`.

## Non-goals

- Adding new RPCs or functionality beyond what v1 provides.
- Changing internal actor architecture or message types.
- Optimistic concurrency (etag/versioning) on config mutations.
- Streaming variants for ops (GetStatus, GetTargets).

## Capabilities

### New Capabilities

- `grpc-v2-common`: Shared proto types — LocationInfo, ModelInfo, CoverageTier, ParameterValue, forecast points, enrichment payloads. All temporal fields as Timestamp.
- `grpc-v2-weather-service`: WeatherService — GetCatalog, GetForecast, GetEnrichments, StreamForecasts, StreamEnrichments.
- `grpc-v2-admin-service`: AdminService — GetConfig, StreamConfig, SetLocations (replace-all), SetSettings, SetEnrichment, SetBudget. ConfigResponse with budget projection and warnings.
- `grpc-v2-ops-service`: OpsService — GetStatus, GetTargets, TriggerPoll. All temporal fields as Timestamp.

### Modified Capabilities

(None — this is a full replacement, existing v1 specs become historical.)

## Impact

- **Proto files**: Delete `protos/njord/v1/`, create `protos/njord/v2/` with 4 files.
- **Service code**: Delete `ForecastGrpcService.cs` and `ConfigGrpcService.cs`. Create
  `WeatherGrpcService.cs`, `AdminGrpcService.cs`, `OpsGrpcService.cs` under `src/Njord/Grpc/`.
- **Proto mapper**: `EnrichmentProtoMapper` adapts to new namespaces.
- **Service registration**: `NjordApplicationSetup` maps three services instead of two.
- **Tests**: All gRPC specs rewritten for v2 services. Internal actor tests unaffected.
- **Budget**: No polling impact — pure API surface change, same internal data paths.
- **csproj**: Proto include paths change from `v1` to `v2`.
