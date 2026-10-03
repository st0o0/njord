## 1. Proto Definitions

- [x] 1.1 Add `StreamForecasts` RPC and `StreamForecastsRequest` / `ForecastUpdate` messages to `protos/njord/v1/forecast_service.proto`
- [x] 1.2 Add enrichment messages to `protos/njord/v1/forecast_service.proto` — `EnrichmentEvent` (oneof wrapper), `AlertUpdate`, `Alert`, `AlertType` enum, `AlertSeverity` enum, `IndexUpdate`, `TrendUpdate`, `ParameterTrend`, `EnergyUpdate`, `DerivedUpdate`, `HorizonDerived`, `ScalarDerived`, `HistoryUpdate`, `ModelMetrics`, `ConsensusUpdate`, `ParameterConsensus`, `HorizonConsensus`
- [x] 1.3 Add `StreamEnrichments` RPC and `StreamEnrichmentsRequest` to `protos/njord/v1/forecast_service.proto`
- [x] 1.4 Add `GetEnrichments` RPC and `GetEnrichmentsRequest` / `GetEnrichmentsResponse` to `protos/njord/v1/forecast_service.proto`
- [x] 1.5 Create `protos/njord/v1/config_service.proto` with `ConfigService` (`GetConfig`, `StreamConfig`), `NjordConfig`, `LocationConfig`, `ParameterConfig`, `EnrichmentConfig` messages
- [x] 1.6 Add `<Protobuf>` for `config_service.proto` to `src/Njord/Njord.csproj` if not already covered by wildcard
- [x] 1.7 Verify `dotnet build` generates stubs for both proto files without errors

## 2. EnrichmentSnapshotStore

- [x] 2.1 Create `EnrichmentSnapshot` record in `src/Njord/Grpc/` — wraps `(string Location, string TypeName, object Result, DateTimeOffset UpdatedAt)`
- [x] 2.2 Create `EnrichmentSnapshotStore` class in `src/Njord/Grpc/` — `ConcurrentDictionary<(string Location, string TypeName), EnrichmentSnapshot>`, `Update()`, `TryGet()`, `GetAll(location)` methods
- [x] 2.3 Create `EnrichmentSnapshotConsumerActor` in `src/Njord/Grpc/` — subscribes to EgressActor BroadcastHub, filters `EnrichmentUpdate`, updates store
- [x] 2.4 Register `EnrichmentSnapshotStore` as singleton and `EnrichmentSnapshotConsumerActor` in `src/Njord/Configuration/NjordServiceSetup.cs` and `NjordActorSystemSetup.cs`
- [x] 2.5 Unit tests for `EnrichmentSnapshotStore` in `src/Njord.Tests/Grpc/` — update, overwrite, GetAll, unknown location

## 3. Domain → Proto Mappers

- [x] 3.1 Create `EnrichmentProtoMapper` static class in `src/Njord/Grpc/` — maps `AlertResult` → `AlertUpdate`, `IndexResult` → `IndexUpdate`, `TrendResult` → `TrendUpdate`, `EnergyResult` → `EnergyUpdate`, `DerivedResult` → `DerivedUpdate`, `HistoryResult` → `HistoryUpdate`, `ConsensusResult` → `ConsensusUpdate`
- [x] 3.2 Unit tests for `EnrichmentProtoMapper` in `src/Njord.Tests/Grpc/` — one test per enrichment type verifying field mapping

## 4. StreamForecasts Implementation

- [x] 4.1 Add `StreamForecasts` method to `ForecastGrpcService` in `src/Njord/Grpc/ForecastGrpcService.cs` — request EgressSourceRef, filter PerModelUpdate, optional location filter, map to ForecastUpdate, write to IServerStreamWriter in a loop
- [x] 4.2 Unit test for StreamForecasts — verify it receives and maps ForecastUpdates (mock IServerStreamWriter)

## 5. StreamEnrichments + GetEnrichments Implementation

- [x] 5.1 Add `GetEnrichments` method to `ForecastGrpcService` — query EnrichmentSnapshotStore, map results via EnrichmentProtoMapper
- [x] 5.2 Add `StreamEnrichments` method to `ForecastGrpcService` — request EgressSourceRef, filter EnrichmentUpdate, map via EnrichmentProtoMapper, write to IServerStreamWriter
- [x] 5.3 Unit tests for GetEnrichments — returns data, returns empty for no data, NOT_FOUND for unknown location
- [x] 5.4 Unit test for StreamEnrichments — verify it receives and maps enrichment events

## 6. ConfigService Implementation

- [x] 6.1 Create `ConfigGrpcService` in `src/Njord/Grpc/` implementing `ConfigService.ConfigServiceBase` — inject `IOptions<NjordOptions>`
- [x] 6.2 Implement `GetConfig` — map NjordOptions to NjordConfig proto message
- [x] 6.3 Implement `StreamConfig` — send current config immediately, then wait for config changes (use IOptionsMonitor for change notifications)
- [x] 6.4 Register `ConfigGrpcService` via `MapGrpcService` in `src/Njord/Configuration/NjordApplicationSetup.cs`
- [x] 6.5 Unit tests for ConfigGrpcService — GetConfig returns correct config, StreamConfig sends initial snapshot

## 7. Validation

- [x] 7.1 Run unit tests: `dotnet run --project Njord.Tests/Njord.Tests.csproj` from `src/`
- [x] 7.2 Run integration tests: `dotnet run --project Njord.Tests.Integration/Njord.Tests.Integration.csproj` from `src/`
- [x] 7.3 Run E2E tests: `dotnet run --project Njord.Tests.Integration.E2E/Njord.Tests.Integration.E2E.csproj` from `src/`
- [x] 7.4 Run slopwatch: `dotnet slopwatch` from repo root
