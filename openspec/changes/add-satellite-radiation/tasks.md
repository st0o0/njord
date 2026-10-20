All test projects run sequentially from `src/`: `dotnet run --project <Project>.Tests/<Project>.Tests.csproj`. Implementation is TDD (red first); xUnit v3, `Spec` suffix, sealed classes, `TestContext.Current.CancellationToken`, `FakeTimeProvider`. Load `njord-enrichment-feature` before section 3 and `njord-actor-spec` before writing handler/hosted-service specs (never create an `ActorSystem` by hand). Run `dotnet format Njord.slnx` after editing `.cs` files.

## 1. Satellite Client

- [ ] 1.1 Verify the Open-Meteo Satellite Radiation API (variables, units, tilt/azimuth parameters and azimuth convention, coverage, update cadence) and whether the Ensemble API offers GTI; record outcomes in design D4 and D6
- [ ] 1.2 Create `SatelliteRadiationResponse` in `src/Njord.OpenMeteo/SatelliteRadiationResponse.cs`; add to `OpenMeteoJsonContext`
- [ ] 1.3 Create `ISatelliteRadiationClient` + `SatelliteRadiationClient` in `src/Njord.OpenMeteo/` using `OpenMeteoRequestBuilder` (thin: variables, tilt, azimuth passed in), base address `https://satellite-radiation-api.open-meteo.com/`
- [ ] 1.4 Register in `src/Njord.OpenMeteo/Configuration/OpenMeteoSetupContainer.cs` via `AddTypedClient` (metrics handler + standard resilience)
- [ ] 1.5 Tests in `src/Njord.OpenMeteo.Tests`: URL building incl. tilt/azimuth, response parsing, null handling
- [ ] 1.6 Validate: `dotnet run --project Njord.OpenMeteo.Tests/Njord.OpenMeteo.Tests.csproj`

## 2. Service and Store

- [ ] 2.1 Create the interval options class (default 60 min, minimum 15 min) in `src/Njord.Core/Configuration/` with validator, registered via `AddNjordOptions<T>`
- [ ] 2.2 Create `ISatelliteRadiationStore` + `SatelliteRadiationStore` in `src/Njord.Core/Domain/` (thread-safe, per location)
- [ ] 2.3 Create `SatelliteRadiationResponseMappings` in `src/Njord.OpenMeteo/Mapping/` (`ToDomain`, stateless)
- [ ] 2.4 Create `SatelliteRadiationService` (`BackgroundService`) in `src/Njord.OpenMeteo/`: toggle-gated, hourly per location, jittered first run, 15 min retry keeping previous data, skips locations without coordinates, `TimeProvider` only
- [ ] 2.5 Register service and store in `OpenMeteoSetupContainer`; confirm whether budget accounting/`BudgetCalculator` needs a hook for out-of-band calls
- [ ] 2.6 Tests: interval, disabled toggle, retry keeps previous, no coordinates, `FakeTimeProvider`-driven loop (no `Thread.Sleep`)
- [ ] 2.7 Validate: `dotnet run --project Njord.OpenMeteo.Tests/Njord.OpenMeteo.Tests.csproj`, `Njord.Core.Tests/Njord.Core.Tests.csproj`

## 3. SatelliteRadiationPoint and Toggle

- [ ] 3.1 Create `SatelliteRadiationPoint` in `src/Njord.Forecast/Solar/SatelliteRadiationPoint.cs` (`IHasValidTime`, all `double?`)
- [ ] 3.2 Add `SatelliteRadiation` to `src/Njord.Core/Configuration/EnrichmentOptions.cs` (default false); update the `EnrichmentConfigKeySpec` allow-list if needed
- [ ] 3.3 Tests in `src/Njord.Forecast.Tests` and `src/Njord.Core.Tests`
- [ ] 3.4 Decide and implement validation of the toggle without `SolarYield`
- [ ] 3.5 Validate: `dotnet run --project Njord.Forecast.Tests/Njord.Forecast.Tests.csproj`, `Njord.Core.Tests`, `Njord.Architecture.Tests`

## 4. SolarYield Blend

- [ ] 4.1 Write failing specs in `src/Njord.Weather.Tests` for: blend at transition, no satellite data equals current output, stale (over 3 h) satellite ignored, GTI without orientation factor
- [ ] 4.2 Inject `ISatelliteRadiationStore` into `src/Njord.Weather/Lifestyle/SolarYieldHandler.cs` and implement the blend behind a per-hour irradiance abstraction (design D6)
- [ ] 4.3 Update registration in `src/Njord.Weather/ServiceCollectionExtensions.cs` if the constructor changes
- [ ] 4.4 Validate: `dotnet run --project Njord.Weather.Tests/Njord.Weather.Tests.csproj`

## 5. Wire Mappings

- [ ] 5.1 Decide whether the source mix is exposed; if so add a nullable/defaulted member to `SolarYieldComputed` and flatten it in `src/Njord.Messages/Pipeline/LifestyleMappings.cs` (explicit wire-name switch)
- [ ] 5.2 Mapping specs in `Njord.Messages.Tests`, `Njord.Grpc.Tests`, `Njord.Mqtt.Tests`
- [ ] 5.3 Validate: `dotnet run --project Njord.Messages.Tests/Njord.Messages.Tests.csproj`, `Njord.Grpc.Tests`, `Njord.Mqtt.Tests`

## 6. Config and Docs

- [ ] 6.1 Config Builder: `docs/.vitepress/theme/builder/types.ts`, `budget.ts`, `serializer.ts`, `ConfigBuilder.vue`, `docs/data/enrichment.json` (toggle plus out-of-band call cost note)
- [ ] 6.2 `docker-compose.example.yml`, `src/Njord/appsettings.Example.json`, SolarYield docs page (API cost, coverage, GTI)
- [ ] 6.3 Final validation from `src/`: `dotnet build Njord.slnx`, `dotnet format Njord.slnx --verify-no-changes`, every test project sequentially via `dotnet run --project`; from the repo root `openspec validate add-satellite-radiation --no-interactive`
