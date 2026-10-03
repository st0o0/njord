## 1. EgressEvent Shape Change

- [x] 1.1 Change `EgressEvent.PerModelUpdate` in `src/Njord/Egress/EgressEvent.cs` — replace `IReadOnlyDictionary<string, string> HorizonPayloads` with `ModelForecast Forecast`
- [x] 1.2 Update `ModelStateActor` in `src/Njord/Egress/ModelStateActor.cs` — remove `HorizonProjection` call, remove delta-dedup cache, remove JSON serialization, emit `PerModelUpdate(location, model, forecast)` directly from the `FetchOutcome.Success`. Keep capability tracking (`ModelCapabilityLearned`).
- [x] 1.3 Update `MqttEgressActor` in `src/Njord/Mqtt/MqttEgressActor.cs` — add `HorizonProjection.BuildPerHorizon` call for `PerModelUpdate`, add per-horizon delta-dedup cache (moved from ModelStateActor), produce `MqttMessage` per changed horizon

## 2. Snapshot Actor Messages

- [x] 2.1 Create snapshot actor messages in `src/Njord/Grpc/SnapshotMessages.cs` — `UpdateForecast(Location, Model, Forecast)`, `UpdateEnrichment(Location, TypeName, Result)`, `Ack`, `GetForecast(Location, ModelId)`, `GetAllForecasts`, `GetEnrichment(Location, TypeName)`, `GetAllEnrichments(Location)`, response records

## 3. Persistent Snapshot Actors

- [x] 3.1 Create `ForecastSnapshotActor` in `src/Njord/Grpc/ForecastSnapshotActor.cs` — `ReceivePersistentActor` with PersistenceId `"forecast-snapshot"`, holds `Dictionary<(string, string), ModelForecast>`, handles `UpdateForecast` (update + SaveSnapshot + Ack), handles `GetForecast`/`GetAllForecasts` queries, recovers from `SnapshotOffer`
- [x] 3.2 Create `EnrichmentSnapshotActor` in `src/Njord/Grpc/EnrichmentSnapshotActor.cs` — same pattern, PersistenceId `"enrichment-snapshot"`, holds `Dictionary<(string, string), object>`, handles `UpdateEnrichment`/`GetEnrichment`/`GetAllEnrichments`
- [x] 3.3 Create new `SnapshotConsumerActor` in `src/Njord/Grpc/GrpcSnapshotConsumerActor.cs` — subscribes to EgressActor BroadcastHub, routes `PerModelUpdate` to `ForecastSnapshotActor` via `Ask<Ack>`, routes `EnrichmentUpdate` to `EnrichmentSnapshotActor` via `Ask<Ack>`
- [x] 3.4 Register all three actors in `src/Njord/Configuration/NjordActorSystemSetup.cs` — `ForecastSnapshotActor`, `EnrichmentSnapshotActor`, new `GrpcSnapshotConsumerActor`; remove old `SnapshotConsumerActor` and `EnrichmentSnapshotConsumerActor` registrations

## 4. Delete Old Stores and Consumer Actors

- [x] 4.1 Delete `src/Njord/Grpc/ForecastSnapshotStore.cs`
- [x] 4.2 Delete `src/Njord/Grpc/ForecastSnapshot.cs`
- [x] 4.3 Delete `src/Njord/Grpc/EnrichmentSnapshotStore.cs`
- [x] 4.4 Delete `src/Njord/Grpc/EnrichmentSnapshot.cs`
- [x] 4.5 Delete old `src/Njord/Grpc/SnapshotConsumerActor.cs` (the one that parses JSON)
- [x] 4.6 Delete `src/Njord/Grpc/EnrichmentSnapshotConsumerActor.cs`
- [x] 4.7 Remove `ForecastSnapshotStore` and `EnrichmentSnapshotStore` singleton registrations from `src/Njord/Configuration/NjordServiceSetup.cs`

## 5. Update gRPC Service

- [x] 5.1 Update `ForecastGrpcService` in `src/Njord/Grpc/ForecastGrpcService.cs` — replace store injection with `ActorRegistry` lookup of snapshot actors, use Ask for `GetForecast`/`GetAllForecasts`/`GetAllEnrichments`, map `ModelForecast` → Proto directly (remove `ForecastSnapshot`/`HourlySnapshotPoint` mapping), update `StreamForecasts` to use typed `ModelForecast` from BroadcastHub
- [x] 5.2 Update `StreamForecasts` handler — map `ModelForecast` → `ForecastUpdate` proto directly (no JSON parsing via `SnapshotConsumerActor.ParseSnapshot`)
- [x] 5.3 Update `GetEnrichments` — Ask `EnrichmentSnapshotActor` instead of reading store

## 6. Update Tests

- [x] 6.1 Update `src/Njord.Tests/Egress/ModelStateActorSpec.cs` — adapt to new `PerModelUpdate` shape (typed `ModelForecast` instead of JSON payloads), remove JSON-related assertions
- [x] 6.2 Create `src/Njord.Tests/Grpc/ForecastSnapshotActorSpec.cs` — test store/retrieve, overwrite, unknown returns null, Ack on update
- [x] 6.3 Create `src/Njord.Tests/Grpc/EnrichmentSnapshotActorSpec.cs` — test store/retrieve, GetAll, Ack on update
- [x] 6.4 Update `src/Njord.Tests/Grpc/ForecastGrpcServiceSpec.cs` — adapt to Ask-based queries instead of store reads
- [x] 6.5 Delete `src/Njord.Tests/Grpc/ForecastSnapshotStoreSpec.cs`
- [x] 6.6 Delete `src/Njord.Tests/Grpc/EnrichmentSnapshotStoreSpec.cs`
- [x] 6.7 Delete `src/Njord.Tests/Grpc/SnapshotConsumerActorSpec.cs`
- [x] 6.8 Update `src/Njord.Tests/Egress/HorizonProjectionSpec.cs` if needed (should still work as HorizonProjection is unchanged)

## 7. Validation

- [x] 7.1 Run unit tests: `dotnet run --project Njord.Tests/Njord.Tests.csproj` from `src/`
- [x] 7.2 Run integration tests: `dotnet run --project Njord.Tests.Integration/Njord.Tests.Integration.csproj` from `src/`
- [x] 7.3 Run E2E tests: `dotnet run --project Njord.Tests.Integration.E2E/Njord.Tests.Integration.E2E.csproj` from `src/`
- [x] 7.4 Run slopwatch: `dotnet slopwatch` from repo root
