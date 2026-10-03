## 1. Remove old ConsensusEnrichment

- [x] 1.1 Delete `src/Njord/Enrichment/Features/ConsensusEnrichment.cs`.
- [x] 1.2 Rename `src/Njord/Enrichment/Features/HourlyConsensusEnrichment.cs` to `ConsensusEnrichment.cs`. Rename class to `ConsensusEnrichment`. Change `TypeName` to `"consensus"`. Change config source from `HourlyConsensus.Enabled` to `Consensus.Enabled`.

## 2. Simplify Config

- [x] 2.1 Remove `HourlyConsensusOptions` from `src/Njord/Configuration/EnrichmentOptions.cs` and the `HourlyConsensus` property from `EnrichmentOptions`.

## 3. Simplify DI Registration

- [x] 3.1 Update `src/Njord/Configuration/NjordServiceSetup.cs`: remove duplicate registration, keep single `ConsensusEnrichment`.

## 4. Revert Proto

- [x] 4.1 Remove `hourly_consensus` fields from `protos/njord/v1/forecast_service.proto` (field 9 in `GetEnrichmentsResponse`, field 17 in `EnrichmentEvent`).

## 5. Simplify gRPC Mapper and Service

- [x] 5.1 Remove `"hourly-consensus"` case from `EnrichmentProtoMapper.MapToEvent()` in `src/Njord/Grpc/EnrichmentProtoMapper.cs`.
- [x] 5.2 Remove `HourlyConsensus` case from `GetEnrichments` switch in `src/Njord/Grpc/ForecastGrpcService.cs`.

## 6. Revert StatePayloadBuilder

- [x] 6.1 Remove `topicSegment` parameter from `StatePayloadBuilder.FromConsensus()` in `src/Njord/Mqtt/StatePayloadBuilder.cs`. Hardcode `"consensus"` back.

## 7. Update Tests

- [x] 7.1 Rename `src/Njord.Tests/Enrichment/HourlyConsensusEnrichmentSpec.cs` to `ConsensusEnrichmentSpec.cs`, update class name and constructor to use `ConsensusOptions` instead of `HourlyConsensusOptions`.
- [x] 7.2 Remove `hourly-consensus` spec from `openspec/specs/` (will be replaced by updated consensus spec on archive sync).

## 8. Validation

- [x] 8.1 Run all tests: `dotnet run --project Njord.Tests/Njord.Tests.csproj` from `src/`.
- [x] 8.2 Run slopwatch: `dotnet slopwatch` from repo root.
