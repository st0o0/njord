All test projects run sequentially from `src/`: `dotnet run --project <Project>.Tests/<Project>.Tests.csproj`. Implementation is TDD (red first); xUnit v3, `Spec` suffix, sealed classes, `[Fact(Timeout = 5000)]`, BDD-style names, `TestContext.Current.CancellationToken`. Load `njord-enrichment-feature` before section 3 and `njord-actor-spec` before handler specs. Run `dotnet format Njord.slnx` after editing `.cs` files.

## 1. Flags and DTOs

- [ ] 1.1 Verify against https://open-meteo.com/en/docs/marine-weather-api the exact names, units, peak-period semantics and `sea_level_height_msl` coverage, and that call weight does not depend on variable count; record in design D1/D5
- [ ] 1.2 Add the flags to `src/Njord.Forecast/Marine/MarineParams.cs`
- [ ] 1.3 Add the response properties to `MarineHourlyData` in `src/Njord.OpenMeteo/MarineResponse.cs`
- [ ] 1.4 Specs: distinct combinable flag values in `src/Njord.Forecast.Tests/`, DTO deserialization in `src/Njord.OpenMeteo.Tests/`
- [ ] 1.5 Validate: `dotnet run --project Njord.Forecast.Tests/Njord.Forecast.Tests.csproj`, `dotnet run --project Njord.OpenMeteo.Tests/Njord.OpenMeteo.Tests.csproj`

## 2. Mappings and Point

- [ ] 2.1 Add nullable members and `HasFields` arms to `src/Njord.Forecast/Marine/MarinePoint.cs`
- [ ] 2.2 Add one arm per flag to `src/Njord.OpenMeteo/Mapping/MarineParamsMappings.cs` and map values in `src/Njord.OpenMeteo/Mapping/MarineResponseMappings.cs`
- [ ] 2.3 Specs per member in `src/Njord.OpenMeteo.Tests/Mapping/` (params and response mapping incl. nulls, combined values unchanged) and `src/Njord.Forecast.Tests/Marine/`
- [ ] 2.4 Validate: `dotnet run --project Njord.Forecast.Tests/Njord.Forecast.Tests.csproj`, `dotnet run --project Njord.OpenMeteo.Tests/Njord.OpenMeteo.Tests.csproj`

## 3. SurfConditions Handler

- [ ] 3.1 Write failing specs in `src/Njord.Marine.Tests/` for `SurfConditionsRule` (swell period bands, height band, offshore/cross/onshore, wind-wave share, no wind, null swell) then implement `SurfConditionsRule` in `src/Njord.Marine/`
- [ ] 3.2 Add `SurfConditions` to `src/Njord.Core/Configuration/EnrichmentOptions.cs`; add the lifestyle event/kind in `src/Njord.Messages/Pipeline/` with explicit wire name in `LifestyleMappings.ToLifestyleUpdate()`
- [ ] 3.3 Create `SurfConditionsHandler` in `src/Njord.Marine/` (`ConfigKey` `SurfConditions`, required swell flags, optional wind-wave flags and Weather wind direction) and register it in `src/Njord.Marine/ServiceCollectionExtensions.cs`
- [ ] 3.4 Handler specs (toggle off, event emitted, no wind data) per `njord-actor-spec`; `EnrichmentConfigKeySpec` in `src/Njord.Architecture.Tests/`; verify `SwimmingConditionsHandler` specs stay green untouched
- [ ] 3.5 Validate: `dotnet run --project Njord.Marine.Tests/Njord.Marine.Tests.csproj`, `dotnet run --project Njord.Messages.Tests/Njord.Messages.Tests.csproj`, `dotnet run --project Njord.Core.Tests/Njord.Core.Tests.csproj`, `dotnet run --project Njord.Architecture.Tests/Njord.Architecture.Tests.csproj`

## 4. Wire, Config and Docs

- [ ] 4.1 Extend Marine payloads additively: `protos/njord/v2/weather.proto`, `src/Njord.Grpc/Mapping/`, `src/Njord.Mqtt/` payloads; golden masters in `Njord.Grpc.Tests` and `Njord.Mqtt.Tests`, lifestyle kind coverage in `Njord.Messages.Tests`
- [ ] 4.2 Config Builder: `docs/.vitepress/theme/builder/types.ts`, `budget.ts`, `serializer.ts`, `ConfigBuilder.vue`, `docs/data/enrichment.json` (SurfConditions toggle, triggers Marine and optionally Weather)
- [ ] 4.3 `docker-compose.example.yml`, `src/Njord/appsettings.Example.json`, marine docs page (swell vs wind waves, scoring)
- [ ] 4.4 Final validation from `src/`: `dotnet build Njord.slnx`, `dotnet format Njord.slnx --verify-no-changes`, every test project sequentially via `dotnet run --project`; from the repo root `openspec validate expand-marine --no-interactive`
