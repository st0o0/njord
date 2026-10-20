All test projects run sequentially from `src/`: `dotnet run --project <Project>.Tests/<Project>.Tests.csproj`. Implementation is TDD; xUnit v3, `Spec` suffix, sealed classes, `TestContext.Current.CancellationToken`, `FakeTimeProvider`. Run `dotnet format Njord.slnx` after editing `.cs` files.

## 1. Domain Vocabulary

- [x] 1.1 Verify the Open-Meteo satellite API (endpoint, variables, tilt/azimuth convention, nowcast window) and record it in design D1
- [x] 1.2 Add `DomainKind.Satellite`, `SatelliteRadiationPoint` and `SatelliteParams` in `src/Njord.Forecast/Solar/`
- [x] 1.3 Add `Satellite`/`OptionalSatellite` to `DomainRequirements`, `LocationForecast.Satellite`, `FeatureKind.SatelliteRadiation` + config key
- [x] 1.4 Add `SatelliteOptions` + validator; `BudgetCalculator` counts the domain per poll cycle
- [x] 1.5 Tests in `Njord.Forecast.Tests` and `Njord.Core.Tests`

## 2. Client and Gateway

- [x] 2.1 `SatelliteRadiationResponse`, `ISatelliteRadiationClient`, `SatelliteRadiationClient`, JSON context, registration in `OpenMeteoSetupContainer`
- [x] 2.2 `SatelliteParamsMappings` and `SatelliteRadiationResponseMappings`; gateway dispatch; telemetry tags
- [x] 2.3 `FakeSatelliteRadiationClient` in `Njord.Tests.Shared`; gateway constructor updates
- [x] 2.4 Tests: URL with tilt/azimuth, parsing, null handling, gateway routing, `PipelineEventMappings`

## 3. Blend and Handlers

- [x] 3.1 `SolarIrradianceBlend` in `src/Njord.Weather/Lifestyle/`
- [x] 3.2 `SatelliteRadiationHandler` + registration; `SolarYieldHandler` uses the blend
- [x] 3.3 Specs: blend at transition, no satellite data, stale data, observed event, feature resolution

## 4. Wire

- [x] 4.1 `PipelineEvent.SatelliteRadiationComputed`, `LifestyleKind.SatelliteRadiation`, `LifestyleMappings`
- [x] 4.2 Proto `DOMAIN_SATELLITE`, `SatelliteData`/`SatelliteSample`, `WireEnumMappings`, `DomainDataProtoMappings`
- [x] 4.3 Mapping specs in `Njord.Messages.Tests` and `Njord.Grpc.Tests`

## 5. Config and Docs

- [x] 5.1 Config Builder: `types.ts` domain, `docs/data/features.json` entry (cost derives from the domain)
- [x] 5.2 `docker-compose.example.yml`, `docs/configuration/index.md`, `AGENTS.md`
- [x] 5.3 Final validation from `src/`: `dotnet build Njord.slnx`, `dotnet format Njord.slnx --verify-no-changes`, every test project sequentially via `dotnet run --project`; from the repo root `openspec validate add-satellite-radiation --no-interactive`
