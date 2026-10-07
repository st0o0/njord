## 1. IActorRegistration Interface

- [x] 1.1 Define `IActorRegistration` interface in `src/Njord.Core/Actors/IActorRegistration.cs` with `void Configure(AkkaConfigurationBuilder builder, IServiceProvider provider)` method
- [x] 1.2 Add unit test in `src/Njord.Core.Tests/` verifying the interface is discoverable and lives in the expected namespace

## 2. Domain Setup Containers — Foundation

- [x] 2.1 Create `CoreSetupContainer` in `src/Njord/Configuration/CoreSetupContainer.cs` — options, TimeProvider, health state, metrics, health checks, ConfigPersistence, Prometheus adapter
- [x] 2.2 Create `IngestSetupContainer` in `src/Njord.Ingest/Configuration/IngestSetupContainer.cs` — `IOpenMeteoClient` + HTTP client, no duplicate `TimeProvider.System`
- [x] 2.3 Create `SensorSetupContainer` in `src/Njord.Sensors/Configuration/SensorSetupContainer.cs` — `IActorRegistration` for `SensorHubActor` (singleton)

## 3. Domain Setup Containers — Pipeline & Egress

- [x] 3.1 Create `PipelineSetupContainer` in `src/Njord.Pipeline/Configuration/PipelineSetupContainer.cs` — budget services, `IActorRegistration` for Scheduler/BudgetTracker/Pipeline actors + shutdown task
- [x] 3.2 Create `EgressSetupContainer` in `src/Njord.Egress/Configuration/EgressSetupContainer.cs` — `IActorRegistration` for `ModelStateActor` (singleton)

## 4. Domain Setup Containers — Enrichment, MQTT, gRPC

- [x] 4.1 Create `EnrichmentSetupContainer` in `src/Njord.Enrichment/Configuration/EnrichmentSetupContainer.cs` — options, validators, computers, features, `IActorRegistration` for `EnrichmentActor` + `ForecastHistoryActor` shard
- [x] 4.2 Create `MqttSetupContainer` in `src/Njord.Mqtt/Configuration/MqttSetupContainer.cs` — `MqttOptions`, presenters, conditional `IActorRegistration` for MQTT actors
- [x] 4.3 Create `GrpcSetupContainer` in `src/Njord.Grpc/Configuration/GrpcSetupContainer.cs` — `AddGrpc()`, `IActorRegistration` for snapshot actors + `GrpcSnapshotConsumerActor`

## 5. Slim AkkaSetupContainer

- [x] 5.1 Refactor `NjordActorSystemSetup` → `AkkaSetupContainer` in `src/Njord/Configuration/AkkaSetupContainer.cs` — infra only + `IActorRegistration` resolution
- [x] 5.2 Remove all direct actor registrations from `AkkaSetupContainer`

## 6. Program.cs & Cleanup

- [x] 6.1 Update `src/Njord/Program.cs` to chain all domain containers
- [x] 6.2 Delete `NjordServiceSetup` (`src/Njord/Configuration/NjordServiceSetup.cs`)
- [x] 6.3 Delete `AddNjordPipeline` extension method (`src/Njord.Pipeline/`)
- [x] 6.4 Delete `AddNjordEnrichment` extension method (`src/Njord.Enrichment/`)
- [x] 6.5 Delete `AddNjordMqtt` extension method (`src/Njord.Mqtt/`)
- [x] 6.6 Delete `AddNjordGrpc` extension method (`src/Njord.Grpc/`)
- [x] 6.7 Delete `AddNjordIngest` extension method (`src/Njord.Ingest/`)

## 7. Snapshot Actor Sharding

- [x] 7.1 Refactor `ForecastSnapshotActor` (`src/Njord.Grpc/`) from singleton (dictionary state) to ShardRegion entity (single-value state, entity-derived PersistenceId, passivation)
- [x] 7.2 Refactor `EnrichmentSnapshotActor` (`src/Njord.Grpc/`) from singleton (dictionary state) to ShardRegion entity (single-value state, entity-derived PersistenceId, passivation)
- [x] 7.3 Update `ForecastSnapshotDto` (`src/Njord.Persistence/`) if needed for single-value entity state — no change needed, reuses existing DTO with single entry
- [x] 7.4 Update `NjordMessageExtractor` (`src/Njord.Core/`) to handle `IWithModelKey` and `IWithEnrichmentKey` routing — already handled, no change needed

## 8. Fan-out Queries

- [x] 8.1 QueryAllForecasts fan-out — not needed in prod (only used in tests, removed with singleton behavior)
- [x] 8.2 QueryAllEnrichments fan-out — implemented in `WeatherGrpcService` via parallel `Task.WhenAll` over `QueryEnrichment` per enrichment type
- [x] 8.3 Update `WeatherGrpcService` — `GetForecast` uses `IForecastSnapshotRegion`, `GetEnrichments` fans out via `IEnrichmentSnapshotRegion`

## 9. Test Updates

- [x] 9.1 Update `ForecastSnapshotActor` specs (`src/Njord.Grpc.Tests/`) — entityId parameter, per-entity queries, removed QueryAllForecasts test
- [x] 9.2 Update `EnrichmentSnapshotActor` specs (`src/Njord.Grpc.Tests/`) — entityId parameter, per-entity queries, removed QueryAllEnrichments test
- [x] 9.3 Update `WeatherGrpcServiceSpec` and `GrpcSnapshotConsumerTerminatedSpec` — region interfaces, per-entity fake actors
- [x] 9.4 No `NjordFixture`/IntegrationTests project exists — skipped
- [x] 9.5 Architecture tests updated (`NjordArchitecture.cs` assembly reference) — all 29 arch tests pass

## 10. Validation

- [x] 10.1 Build solution: 0 errors, 0 warnings
- [x] 10.2 Run all test projects: 817/817 pass (2 removed: singleton QueryAll tests)
- [x] 10.3 Slopwatch — no tool manifest in repo, skipped
- [x] 10.4 Format whitespace check: clean
- [x] 10.5 OpenSpec validation: 91/103 pass (12 pre-existing failures, none from this change)
