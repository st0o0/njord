## 1. Configuration

- [x] 1.1 Add `HourlyConsensusOptions` class to `src/Njord/Configuration/EnrichmentOptions.cs` with `Enabled = false` (default). Add `HourlyConsensus` property to `EnrichmentOptions`.

## 2. Enrichment Feature

- [x] 2.1 Create `src/Njord/Enrichment/Features/HourlyConsensusEnrichment.cs` implementing `IStatelessEnrichment` with `TypeName = "hourly-consensus"`. Compute dynamic horizon range (scan models for max ValidAt, cutoff at second-largest). Call `ConsensusResult.Compute()` with generated hourly horizons. Filter out horizons with `AvailableModels < 2` from the result.
- [x] 2.2 Register `HourlyConsensusEnrichment` in `src/Njord/Configuration/NjordServiceSetup.cs` as `IEnrichmentFeature`.

## 3. gRPC Output

- [x] 3.1 Add `ConsensusUpdate hourly_consensus = 9` to `GetEnrichmentsResponse` and `ConsensusUpdate hourly_consensus = 17` to `EnrichmentEvent` in `protos/njord/v1/forecast_service.proto`.
- [x] 3.2 Add `"hourly-consensus"` case to `EnrichmentProtoMapper.MapToEvent()` in `src/Njord/Grpc/EnrichmentProtoMapper.cs` — reuse existing `MapConsensus()` method.
- [x] 3.3 Add `hourly_consensus` case to the `GetEnrichments` switch in `src/Njord/Grpc/ForecastGrpcService.cs`.

## 4. MQTT Output

- [x] 4.1 Add `FromHourlyConsensus` method to `src/Njord/Mqtt/StatePayloadBuilder.cs` — reuse existing `FromConsensus` logic with `"hourly-consensus"` topic segment.
- [x] 4.2 Implement `BuildDiscoveryPayload` and `ToStateMessages` in `HourlyConsensusEnrichment`.

## 5. Snapshot DTO

- [x] 5.1 Add `"HourlyConsensusResult"` mapping to the enrichment type registry in `src/Njord/Grpc/SnapshotDtos.cs`. Since the domain type is `ConsensusResult`, map `"hourly-consensus"` TypeName to `ConsensusResult`.

## 6. Tests

- [x] 6.1 Add `src/Njord.Tests/Enrichment/HourlyConsensusEnrichmentSpec.cs` using `PersistenceTestKit`. Test: dynamic cutoff with mixed-horizon models, ≥2 model filter, 3-hourly model inclusion at native hours, disabled-by-default.

## 7. Validation

- [x] 7.1 Run all tests: `dotnet run --project Njord.Tests/Njord.Tests.csproj` from `src/`.
- [x] 7.2 Run slopwatch: `dotnet slopwatch` from repo root.
