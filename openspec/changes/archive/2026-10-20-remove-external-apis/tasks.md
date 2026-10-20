## 1. Remove Geocoding

- [x] 1.1 Delete `src/Njord.OpenMeteo/{LocationResolutionService,GeocodingClient,IGeocodingClient,GeocodingResponse}.cs` and `src/Njord.OpenMeteo.Tests/GeocodingSpec.cs`
- [x] 1.2 Remove `WithName` from `src/Njord.OpenMeteo/OpenMeteoRequestBuilder.cs` and its cases in `src/Njord.OpenMeteo.Tests/OpenMeteoRequestMappingsSpec.cs`
- [x] 1.3 Remove geocoding registrations in `src/Njord.OpenMeteo/Configuration/OpenMeteoSetupContainer.cs` and `GeocodingResponse` in `src/Njord.OpenMeteo/OpenMeteoJsonContext.cs`
- [x] 1.4 Make `Latitude`/`Longitude` required and remove `HasCoordinates` in `src/Njord.Core/Configuration/LocationOptions.cs`; fix users (`src/Njord/Configuration/ServiceInfoMappings.cs`, `src/Njord.Grpc/Mapping/CatalogProtoMappings.cs`, `src/Njord.IntegrationTests/NjordTestHost.cs`)
- [x] 1.5 Validate presence and range in `src/Njord.Core/Configuration/NjordOptionsValidator.cs`; add specs (`[Fact(Timeout = 5000)]`, sealed, BDD names) in `src/Njord.Core.Tests/Configuration/NjordOptionsValidatorSpec.cs`: missing latitude, missing longitude, out of range, valid

## 2. Remove Climate Change API

- [x] 2.1 Delete `src/Njord.OpenMeteo/{ClimateBaselineService,ClimateClient,IClimateClient,ClimateResponse}.cs`, `src/Njord.OpenMeteo/Mapping/ClimateResponseMappings.cs`, `src/Njord.OpenMeteo.Tests/ClimateSpec.cs`
- [x] 2.2 Delete `src/Njord.Core/Domain/{IClimateBaselineStore,MonthlyBaseline}.cs` and the store implementation
- [x] 2.3 Remove climate registrations from `OpenMeteoSetupContainer.cs` and `ClimateResponse` from `OpenMeteoJsonContext.cs`
- [x] 2.4 Delete `src/Njord.Weather/Lifestyle/ClimateAnomalyHandler.cs` and `src/Njord.Weather.Tests/Lifestyle/ClimateAnomalyHandlerSpec.cs`; update `src/Njord.Weather/ServiceCollectionExtensions.cs`
- [x] 2.5 Remove the `ClimateAnomaly` toggle from `src/Njord.Core/Configuration/EnrichmentOptions.cs`; fix `EnrichmentConfigKeySpec` and any toggle-count assertions (20 to 19) in `src/Njord.Core.Tests` and `src/Njord.Architecture.Tests`

## 3. Update wire mappings

- [x] 3.1 Remove `ClimateAnomaly` from `src/Njord.Messages/Pipeline/LifestyleKind.cs` and `LifestyleMappings.cs`; remove `ClimateAnomalyComputed` from `PipelineEvent.cs`
- [x] 3.2 Update `src/Njord.Messages.Tests/LifestyleMappingsSpec.cs` so every remaining member is still pinned
- [x] 3.3 Fix gRPC/MQTT mappings and specs that reference the removed kind (`src/Njord.Grpc.Tests/ProtoMappingsSpec.cs`, `WeatherGrpcServiceSpec.cs`, `src/Njord.Mqtt*`); check ha-njord for `climate_anomaly`

## 4. Update config examples and Config Builder

- [x] 4.1 Update `src/Njord/appsettings.Example.json`, `docker-compose.example.yml`, `e2e/docker-compose.e2e.yml`, `e2e/E2E-TEST-PLAN.md` (coordinates required, no ClimateAnomaly)
- [x] 4.2 Update `docs/.vitepress/theme/builder/{types.ts,ConfigBuilder.vue}`, `docs/data/enrichment.json`, `docs/builder.md` (coordinates mandatory, 19 toggles)
- [x] 4.3 Update `AGENTS.md` (toggle count, hosted services, project descriptions, test counts)

## 5. Validation

- [x] 5.1 `cd src && dotnet build Njord.slnx && dotnet format Njord.slnx --verify-no-changes`
- [x] 5.2 From `src/`: `dotnet run --project Njord.Core.Tests/Njord.Core.Tests.csproj`, and the same for `Njord.Messages.Tests`, `Njord.OpenMeteo.Tests`, `Njord.Weather.Tests`, `Njord.Grpc.Tests`, `Njord.Mqtt.Tests`, `Njord.Architecture.Tests`, `Njord.IntegrationTests`
- [x] 5.3 `grep -rniE "ClimateAnomaly|geocod|ClimateBaseline" src docs openspec/specs e2e` returns nothing outside archive and `seasonal-outlook`
- [x] 5.4 `openspec validate --all --no-interactive`
