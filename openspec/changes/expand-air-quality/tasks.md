All test projects run sequentially from `src/`: `dotnet run --project <Project>.Tests/<Project>.Tests.csproj`. Implementation is TDD (red first); xUnit v3, `Spec` suffix, sealed classes, `[Fact(Timeout = 5000)]`, BDD-style names, `TestContext.Current.CancellationToken`. Load `njord-enrichment-feature` before section 3 and `njord-actor-spec` before handler specs. Run `dotnet format Njord.slnx` after editing `.cs` files.

## 1. Flags and DTOs

- [ ] 1.1 Verify against https://open-meteo.com/en/docs/air-quality-api the exact names, units and regional coverage of `carbon_monoxide`, `sulphur_dioxide`, `dust`, `ammonia`, `olive_pollen`, `us_aqi`, `uv_index`, `uv_index_clear_sky`, and that call weight does not depend on variable count; record in design D1/D2
- [x] 1.2 Add the flags to `src/Njord.Forecast/AirQuality/AirQualityParams.cs`
- [x] 1.3 Add the response properties to `AirQualityHourlyData` in `src/Njord.OpenMeteo/AirQualityResponse.cs`
- [x] 1.4 Specs: distinct combinable flag values in `src/Njord.Forecast.Tests/`, DTO deserialization of the new names in `src/Njord.OpenMeteo.Tests/`
- [x] 1.5 Validate: `dotnet run --project Njord.Forecast.Tests/Njord.Forecast.Tests.csproj`, `dotnet run --project Njord.OpenMeteo.Tests/Njord.OpenMeteo.Tests.csproj`

## 2. Mappings and Point

- [x] 2.1 Add nullable members and `HasFields` arms to `src/Njord.Forecast/AirQuality/AirQualityPoint.cs`
- [x] 2.2 Add one arm per flag to `src/Njord.OpenMeteo/Mapping/AirQualityParamsMappings.cs` and map values in `src/Njord.OpenMeteo/Mapping/AirQualityResponseMappings.cs`
- [x] 2.3 Specs per member in `src/Njord.OpenMeteo.Tests/Mapping/` (params mapping, response mapping incl. nulls) and `src/Njord.Forecast.Tests/AirQuality/`; update `OpenMeteoUriBuilderSpec` if it pins the AQ request
- [x] 2.4 Validate: `dotnet run --project Njord.Forecast.Tests/Njord.Forecast.Tests.csproj`, `dotnet run --project Njord.OpenMeteo.Tests/Njord.OpenMeteo.Tests.csproj`

## 3. Region and Dust Handler

- [ ] 3.1 Add `AirQualityRegion` and `AirQualityOptions.Region` (+ validator, explicit wire text) in `src/Njord.Core/Configuration/`; apply the region filter where AirQuality request flags are composed; specs in `src/Njord.Core.Tests/` and `src/Njord.OpenMeteo.Tests/`
- [ ] 3.2 Add `DustAlert` to `src/Njord.Core/Configuration/EnrichmentOptions.cs`, `AlertType.Dust` in `src/Njord.Messages/Alerts/`, dust thresholds + validation in `AlertOptions`/`AlertOptionsValidator`
- [ ] 3.3 Create `DustAlertRule` and register a `DustAlert` handler in `src/Njord.AirQuality/` (`ServiceCollectionExtensions.cs`); handler requires `Dust`, triggers AirQuality
- [ ] 3.4 Specs in `src/Njord.AirQuality.Tests/` (rule thresholds per step, toggle off), `src/Njord.Architecture.Tests/` (`EnrichmentConfigKeySpec`), `src/Njord.Core.Tests/` (budget unchanged with new flags)
- [ ] 3.5 Validate: `dotnet run --project Njord.AirQuality.Tests/Njord.AirQuality.Tests.csproj`, `dotnet run --project Njord.Core.Tests/Njord.Core.Tests.csproj`, `dotnet run --project Njord.Architecture.Tests/Njord.Architecture.Tests.csproj`

## 4. Wire, Config and Docs

- [ ] 4.1 Extend AirQuality payloads additively: `protos/njord/v2/weather.proto` (new fields, new alert enum value), `src/Njord.Grpc/Mapping/`, `src/Njord.Mqtt/` payloads and `AlertType` wire-name switches; golden masters in `Njord.Messages.Tests`, `Njord.Grpc.Tests`, `Njord.Mqtt.Tests`
- [ ] 4.2 Config Builder: `docs/.vitepress/theme/builder/types.ts`, `budget.ts`, `serializer.ts`, `ConfigBuilder.vue`, `docs/data/enrichment.json` (DustAlert toggle, `Region` select)
- [ ] 4.3 `docker-compose.example.yml`, `src/Njord/appsettings.Example.json`, air-quality docs page (variables, regions)
- [ ] 4.4 Final validation from `src/`: `dotnet build Njord.slnx`, `dotnet format Njord.slnx --verify-no-changes`, every test project sequentially via `dotnet run --project`; from the repo root `openspec validate expand-air-quality --no-interactive`
