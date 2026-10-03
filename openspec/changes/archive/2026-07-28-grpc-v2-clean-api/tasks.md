## 1. Proto Files

- [x] 1.1 Create `protos/njord/v2/common.proto` with shared types: LocationInfo, ModelInfo, CoverageTier, ParameterValue, HourlyForecast (Timestamp valid_at), DailyForecast (string date), all enrichment payloads (1:1 from v1)
- [x] 1.2 Create `protos/njord/v2/weather.proto` with WeatherService: GetCatalog, GetForecast, GetEnrichments, StreamForecasts, StreamEnrichments — all temporal fields as Timestamp
- [x] 1.3 Create `protos/njord/v2/admin.proto` with AdminService: GetConfig, StreamConfig, SetLocations, SetSettings, SetEnrichment, SetBudget — reuse config/enrichment messages from common.proto
- [x] 1.4 Create `protos/njord/v2/ops.proto` with OpsService: GetStatus, GetTargets, TriggerPoll — ModelStatus and TriggerTarget with Timestamp fields
- [x] 1.5 Update `src/Njord/Njord.csproj` Protobuf include from `v1/*.proto` to `v2/*.proto`, verify `dotnet build` compiles all generated stubs

## 2. Delete v1

- [x] 2.1 Delete `protos/njord/v1/forecast_service.proto` and `protos/njord/v1/config_service.proto`
- [x] 2.2 Delete `src/Njord/Grpc/ForecastGrpcService.cs` and `src/Njord/Grpc/ConfigGrpcService.cs`
- [x] 2.3 Delete v1 test files: `src/Njord.Tests/Grpc/ForecastGrpcServiceSpec.cs`, `src/Njord.Tests/Grpc/ConfigGrpcServiceSpec.cs`, `src/Njord.Tests/Grpc/ConfigGrpcServiceStatusSpec.cs`, `src/Njord.Tests/Grpc/GetTriggerTargetsSpec.cs`

## 3. WeatherService Implementation

- [x] 3.1 Create `src/Njord/Grpc/WeatherGrpcService.cs` implementing GetCatalog (locations + ModelInfo from config + ModelCoverageRegistry), GetForecast (ask ForecastSnapshotActor), GetEnrichments (ask EnrichmentSnapshotActor)
- [x] 3.2 Add StreamForecasts and StreamEnrichments to WeatherGrpcService (EgressActor source, same stream logic as v1 but with Timestamp mapping)
- [x] 3.3 Update `src/Njord/Grpc/EnrichmentProtoMapper.cs` to use `Njord.Grpc.V2` namespace types
- [x] 3.4 Create `src/Njord.Tests/Grpc/WeatherGrpcServiceSpec.cs` — test GetCatalog (dedup, resolved models), GetForecast (success + NOT_FOUND), GetEnrichments

## 4. AdminService Implementation

- [x] 4.1 Create `src/Njord/Grpc/AdminGrpcService.cs` implementing GetConfig, StreamConfig (same logic as v1 ConfigGrpcService read RPCs, using V2 types)
- [x] 4.2 Add SetLocations to AdminGrpcService — replace-all semantics with budget validation, reject empty list
- [x] 4.3 Add SetSettings, SetEnrichment, SetBudget to AdminGrpcService — partial-update semantics with budget validation (port logic from v1 UpdateForecastSettings, UpdateEnrichmentConfig, UpdateBudget)
- [x] 4.4 Create `src/Njord.Tests/Grpc/AdminGrpcServiceSpec.cs` — test GetConfig, SetLocations (replace-all, budget rejection, empty rejection), SetSettings (partial update, min interval), SetEnrichment, SetBudget (set + clear)

## 5. OpsService Implementation

- [x] 5.1 Create `src/Njord/Grpc/OpsGrpcService.cs` implementing GetStatus (Timestamp fields), GetTargets (Timestamp fields), TriggerPoll
- [x] 5.2 Create `src/Njord.Tests/Grpc/OpsGrpcServiceSpec.cs` — test GetStatus (models, budget, enrichments, scheduler timeout), GetTargets (all pairs, timeout), TriggerPoll (specific, wildcard, unknown)

## 6. Wiring

- [x] 6.1 Update `src/Njord/Configuration/NjordApplicationSetup.cs` to map WeatherGrpcService, AdminGrpcService, OpsGrpcService (replacing ForecastGrpcService, ConfigGrpcService)
- [x] 6.2 Update `src/Njord/Configuration/NjordServiceSetup.cs` if any DI registrations reference old service types

## 7. Validation

- [x] 7.1 Run `dotnet build src/Njord.slnx` — verify clean build with zero errors
- [x] 7.2 Run `dotnet run --project src/Njord.Tests/Njord.Tests.csproj` — verify all tests pass
- [x] 7.3 Run `dotnet slopwatch` from repo root — verify no quality regressions
