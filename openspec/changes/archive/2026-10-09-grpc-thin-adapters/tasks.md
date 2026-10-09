## 1. CatalogMapper extraction

- [x] 1.1 Create `src/Njord.Grpc/CatalogMapper.cs` with static `ToModelInfo(string modelId)` and `ToLocationInfo(LocationOptions location, IReadOnlyList<string> models)`, moving the logic out of `WeatherGrpcService.GetCatalog` (`src/Njord.Grpc/WeatherGrpcService.cs`).
- [x] 1.2 Update `WeatherGrpcService.GetCatalog` to call `CatalogMapper.ToLocationInfo`/`ToModelInfo`, keeping the `seenModels` dedup loop in the service method.
- [x] 1.3 Add `CatalogMapperSpec` in `src/Njord.Grpc.Tests/` covering: model with full `ModelCoverageRegistry` entry, model with no registry entry, all four `CoverageTier` values.
- [x] 1.4 Confirm existing `WeatherGrpcServiceSpec` `GetCatalog` assertions still pass unmodified.

## 2. Forecast point mapping extraction

- [x] 2.1 Add `WeatherGrpcService.ToHourlyForecast(ForecastPoint point) -> HourlyForecast` and `ToDailyForecast(DailyForecastPoint point) -> DailyForecast`, moving the per-field `SetOptional`/conditional logic out of `MapForecastPoints`.
- [x] 2.2 Update `MapForecastPoints` to `hourlyTarget.Add(...)`/`dailyTarget.Add(...)` via `.Select(ToHourlyForecast)`/`.Select(ToDailyForecast)` (or direct loop calling the new functions).
- [x] 2.3 Add `WeatherGrpcServiceSpec` cases covering: a point with all optional fields present, a point with all optional fields absent, and the `Extra` parameter-value fallback for non-fixed fields.
- [x] 2.4 Confirm `GetForecast`/`StreamForecasts` existing specs still pass unmodified (byte-identical proto output).

## 3. PollStatusMapper (OpsGrpcService dedup)

- [x] 3.1 Create `src/Njord.Grpc/PollStatusMapper.cs` with `ToModelStatus(PollStateEntry entry) -> ModelStatus` and `ToTriggerTarget(PollStateEntry entry) -> TriggerTarget`.
- [x] 3.2 Update `OpsGrpcService.GetStatus` (`src/Njord.Grpc/OpsGrpcService.cs`) to build `status.Models` via `snapshot.Entries.Select(PollStatusMapper.ToModelStatus)`.
- [x] 3.3 Update `OpsGrpcService.GetTargets` to build `response.Targets` via `snapshot.Entries.Select(PollStatusMapper.ToTriggerTarget)`.
- [x] 3.4 Add `PollStatusMapperSpec` in `src/Njord.Grpc.Tests/` covering: entry with `LastChangeUtc`/`CycleSeconds` present, entry with both absent, both `PollPhase` values.
- [x] 3.5 Confirm `OpsGrpcServiceSpec` `GetStatus`/`GetTargets` assertions still pass unmodified.

## 4. AdminGrpcService step separation

- [x] 4.1 Split `AdminGrpcService.SetLocations` into named private steps: request mapping, validation, apply. Same for `SetSettings`.
- [x] 4.2 Confirm existing `AdminGrpcServiceSpec`/`OpsGrpcServiceSpec` coverage for `SetLocations`/`SetSettings` still passes unmodified; add a case if validation-failure vs. apply-failure paths aren't already independently covered.

## 5. EnrichmentProtoMapper decomposition

- [x] 5.1 Identify the repeated per-feature inline construction blocks in `src/Njord.Grpc/EnrichmentProtoMapper.cs` (alerts, derived, trends, indices, history, consensus) and extract one named mapper function per feature.
- [x] 5.2 Add/extend `EnrichmentProtoMapperSpec` in `src/Njord.Grpc.Tests/` covering each extracted feature mapper independently.
- [x] 5.3 Confirm existing `GetEnrichments`/`StreamEnrichments` specs still pass unmodified.

## Validation

- [x] 6.1 `dotnet build Njord.slnx` (run from `src/`)
- [x] 6.2 `dotnet run --project Njord.Grpc.Tests/Njord.Grpc.Tests.csproj --no-build`
- [x] 6.3 `dotnet slopwatch analyze -d . --fail-on warning` (run from repo root)
