## 1. Forecast DTOs and Mapping

- [x] 1.1 Create `src/Njord/Grpc/SnapshotDtos.cs` with forecast DTO types: `ForecastSnapshotDto` (`Dictionary<string, ModelForecastDto>`), `ModelForecastDto` (Model, Location, CycleUtc, `ForecastPointDto[]` Hourly, `DailyForecastPointDto[]` Daily), `ForecastPointDto` (ValidAtUtc, `Dictionary<string, double?>` Values), `DailyForecastPointDto` (Date as string, `Dictionary<string, double?>` NumericValues, `Dictionary<string, string?>` MetaValues).
- [x] 1.2 Add static mapping class `SnapshotMapping` in the same file with `ToDto()` / `ToDomain()` extension methods for the forecast types. `ParameterDef` maps to/from `ApiName` string via `ParameterRegistry.GetByApiName()`. Unknown ApiNames are dropped on recovery.

## 2. Enrichment DTOs and Mapping

- [x] 2.1 Add enrichment DTO types to `src/Njord/Grpc/SnapshotDtos.cs`: `EnrichmentSnapshotDto` (`Dictionary<string, EnrichmentEntryDto>`), `EnrichmentEntryDto` (TypeName string, JsonPayload string).
- [x] 2.2 Add enrichment mapping to `SnapshotMapping`: `ToDto()` serializes the enrichment result to JSON with `TypeName` discriminator, `ToDomain()` deserializes based on `TypeName`. Use a static dictionary of known type names → types (`AlertResult`, `IndexResult`, `TrendResult`, `DerivedResult`, `EnergyResult`, `ConsensusResult`). Unknown types are dropped.

## 3. Wire DTOs into Actors

- [x] 3.1 Update `src/Njord/Grpc/ForecastSnapshotActor.cs`: remove private `ForecastSnapshotState` class. `SaveSnapshot` maps `_state` to `ForecastSnapshotDto` via `ToDto()`. `Recover<SnapshotOffer>` deserializes `ForecastSnapshotDto` and maps to domain via `ToDomain()`.
- [x] 3.2 Update `src/Njord/Grpc/EnrichmentSnapshotActor.cs`: remove private `EnrichmentSnapshotState` class. `SaveSnapshot` maps `_state` to `EnrichmentSnapshotDto`. `Recover<SnapshotOffer>` deserializes `EnrichmentSnapshotDto` and maps to domain.

## 4. Tests

- [x] 4.1 Verify existing `ForecastSnapshotRecoverySpec` tests pass (recovery round-trips through DTOs now).
- [x] 4.2 Verify existing `EnrichmentSnapshotRecoverySpec` tests pass.
- [x] 4.3 Verify existing `ForecastSnapshotActorSpec` and `EnrichmentSnapshotActorSpec` CRUD tests pass.

## 5. Validation

- [x] 5.1 Run all tests: `dotnet run --project Njord.Tests/Njord.Tests.csproj` from `src/`.
- [x] 5.2 Run slopwatch: `dotnet slopwatch` from repo root.
