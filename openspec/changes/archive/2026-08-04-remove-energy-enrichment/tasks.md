## 1. Delete energy-only source files

- [x] 1.1 Delete `src/Njord/Domain/Analysis/EnergyForecaster.cs`, `src/Njord/Domain/Analysis/EnergyResult.cs`, `src/Njord/Enrichment/Features/EnergyEnrichment.cs`
- [x] 1.2 Delete `src/Njord.Tests/Domain/Analysis/EnergyForecasterSpec.cs`, `src/Njord.Tests/Domain/Analysis/EnergyResultSpec.cs`, `src/Njord.Tests/Enrichment/Features/EnergyEnrichmentSpec.cs`

## 2. Remove energy from configuration and DI

- [x] 2.1 Remove `EnergyOptions` class and `Energy` property from `src/Njord/Configuration/EnrichmentOptions.cs`
- [x] 2.2 Remove `EnergyOptionsValidator` from `src/Njord/Configuration/EnrichmentOptionsValidation.cs`
- [x] 2.3 Remove energy DI registrations (validator + enrichment feature) from `src/Njord/Configuration/NjordServiceSetup.cs`
- [x] 2.4 Remove energy validation tests from `src/Njord.Tests/Configuration/EnrichmentOptionsValidationSpec.cs`

## 3. Remove energy from MQTT egress

- [x] 3.1 Remove `FromEnergy` method from `src/Njord/Mqtt/StatePayloadBuilder.cs`
- [x] 3.2 Remove `BuildEnergy` method from `src/Njord/Mqtt/DiscoveryPayloadBuilder.cs`
- [x] 3.3 Remove energy tests from `src/Njord.Tests/Mqtt/StatePayloadBuilderSpec.cs` and `src/Njord.Tests/Mqtt/DiscoveryPayloadBuilderSpec.cs`

## 4. Remove energy from gRPC layer

- [x] 4.1 Remove `EnergyUpdate`, `CopOptimalHour` messages from `protos/njord/v2/common.proto` (reserve field numbers)
- [x] 4.2 Remove `EnergyConfig` message and energy fields from `protos/njord/v2/admin.proto` (reserve field numbers)
- [x] 4.3 Remove energy fields from `protos/njord/v2/weather.proto` (reserve field numbers)
- [x] 4.4 Remove `MapEnergy` method and energy switch arm from `src/Njord/Grpc/EnrichmentProtoMapper.cs`
- [x] 4.5 Remove energy config mutation block and response mapping from `src/Njord/Grpc/AdminGrpcService.cs`
- [x] 4.6 Remove energy case from `src/Njord/Grpc/WeatherGrpcService.cs`
- [x] 4.7 Remove energy from active enrichments in `src/Njord/Grpc/OpsGrpcService.cs`
- [x] 4.8 Remove energy gRPC tests from `src/Njord.Tests/Grpc/EnrichmentProtoMapperSpec.cs` and `src/Njord.Tests/Grpc/OpsGrpcServiceSpec.cs`

## 5. Remove energy from persistence and shared tests

- [x] 5.1 Remove `EnergyResult` type mapping from `src/Njord/Persistence/EnrichmentSnapshotDtos.cs`
- [x] 5.2 Remove energy serialization test and re-verify snapshot in `src/Njord.Tests/Persistence/EnrichmentResultSerializationSpec.cs`
- [x] 5.3 Remove energy references from `src/Njord.Tests/Enrichment/EnrichmentFeatureContractSpec.cs` and `src/Njord.Tests/Enrichment/EnrichmentActorSpec.cs`

## 6. Remove energy from configuration files

- [x] 6.1 Remove Energy section from `src/Njord/appsettings.Example.json` and `src/Njord/appsettings.Development.json`

## 7. Build verification

- [x] 7.1 Run `dotnet build Njord.slnx` from `src/` — must compile clean
- [x] 7.2 Run `dotnet run --project Njord.Tests/Njord.Tests.csproj` from `src/` — all tests must pass
- [x] 7.3 Grep for residual energy references: `Energy`, `energy`, `cop_estimate`, `heating_demand`, `shading`, `battery_strategy`, `night_cooling` in `src/` — no hits in source files

## 8. Update documentation and specs

- [x] 8.1 Remove energy from `docs/configuration/enrichment.md`, `docs/data/enrichment.json`, `docs/index.md`, `docs/architecture.md`, `docs/mqtt-reference.md`
- [x] 8.2 Remove energy mentions from `CLAUDE.md` and `README.md`
- [x] 8.3 Remove energy from shared OpenSpec specs (feature lists, examples, cross-references in `openspec/specs/`)
- [x] 8.4 Delete `openspec/specs/energy-management/` directory

## 9. Final verification

- [x] 9.1 Run `dotnet build Njord.slnx` and full test suite one final time after docs/specs cleanup
- [x] 9.2 Run `dotnet slopwatch` from repo root
- [x] 9.3 Run `dotnet format` whitespace check from `src/`
