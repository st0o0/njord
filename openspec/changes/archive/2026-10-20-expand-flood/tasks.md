All test projects run sequentially from `src/`: `dotnet run --project <Project>.Tests/<Project>.Tests.csproj`. Implementation is TDD (red first); xUnit v3, `Spec` suffix, sealed classes, `[Fact(Timeout = 5000)]`, BDD-style names, `TestContext.Current.CancellationToken`. Load `njord-enrichment-feature` before section 3. Run `dotnet format Njord.slnx` after editing `.cs` files. Any change to polling needs a budget estimate (see proposal).

## 1. Ensemble Request and Response

- [x] 1.1 Verify against https://open-meteo.com/en/docs/flood-api and the Open-Meteo pricing/weight rules: `ensemble=true` response shape (member series names and count), available percentiles, effective call weight with ensemble and with `ForecastDays` up to 210; record the weight constant and the D5 fallback decision in design D2/D5
- [x] 1.2 Add `WithEnsemble(bool)` to `src/Njord.OpenMeteo/OpenMeteoRequestBuilder.cs`; pass it through `src/Njord.OpenMeteo/FloodClient.cs` and `IFloodClient.cs`
- [x] 1.3 Add the member series to `FloodDailyData` in `src/Njord.OpenMeteo/FloodResponse.cs`; add `Ensemble` to `src/Njord.Forecast/Flood/FloodParams.cs` and `EnsembleMembers` to `src/Njord.Forecast/Flood/FloodPoint.cs`
- [x] 1.4 Specs: URL building with and without ensemble in `src/Njord.OpenMeteo.Tests/OpenMeteoUriBuilderSpec.cs`, DTO parsing in `src/Njord.OpenMeteo.Tests/`, point/`HasFields` in `src/Njord.Forecast.Tests/Flood/`
- [x] 1.5 Validate: `dotnet run --project Njord.OpenMeteo.Tests/Njord.OpenMeteo.Tests.csproj`, `dotnet run --project Njord.Forecast.Tests/Njord.Forecast.Tests.csproj`

## 2. Mappings, Options and Budget

- [x] 2.1 Add the `Ensemble` arm to `src/Njord.OpenMeteo/Mapping/FloodParamsMappings.cs` and map members per day in `src/Njord.OpenMeteo/Mapping/FloodResponseMappings.cs` (drop nulls)
- [x] 2.2 Add `IncludeEnsemble` (default false) to `src/Njord.Core/Configuration/FloodOptions.cs` (validator unchanged for `ForecastDays` 1 to 210); thread it through the Gateway flood request composition
- [x] 2.3 Add the verified flood weight to `src/Njord.Core/Configuration/Polling/BudgetCalculator.cs` (weight 1 without ensemble) and mirror it in `docs/.vitepress/theme/builder/budget.ts`
- [x] 2.4 Specs in `src/Njord.OpenMeteo.Tests/Mapping/`, `src/Njord.Core.Tests/` (options defaults, validator bounds 210/211, budget projection with and without ensemble)
- [x] 2.5 Validate: `dotnet run --project Njord.OpenMeteo.Tests/Njord.OpenMeteo.Tests.csproj`, `dotnet run --project Njord.Core.Tests/Njord.Core.Tests.csproj`

## 3. Confidence in FloodRisk and Docs

- [x] 3.1 Write failing specs in `src/Njord.Flood.Tests/` (high confidence 45/50, outlier 1/50, no ensemble equals today's alert), then add confidence to `src/Njord.Flood/FloodRiskAlertRule.cs` and optional ensemble requirement in `FloodRiskHandler.cs`
- [x] 3.2 Add an optional confidence member to the flood alert in `src/Njord.Messages/Alerts/` and its additive mapping in `src/Njord.Grpc/Mapping/`, `src/Njord.Mqtt/` payloads and `protos/njord/v2/weather.proto`; golden masters in `Njord.Messages.Tests`, `Njord.Grpc.Tests`, `Njord.Mqtt.Tests`
- [x] 3.3 Config Builder (`docs/.vitepress/theme/builder/types.ts`, `serializer.ts`, `ConfigBuilder.vue`, `docs/data/enrichment.json`), `docker-compose.example.yml`, `src/Njord/appsettings.Example.json`, flood docs page (ensemble cost, horizon trade-off)
- [x] 3.4 Final validation from `src/`: `dotnet build Njord.slnx`, `dotnet format Njord.slnx --verify-no-changes`, `dotnet run --project Njord.Flood.Tests/Njord.Flood.Tests.csproj`, `Njord.Messages.Tests`, `Njord.Grpc.Tests`, `Njord.Mqtt.Tests`, `Njord.Architecture.Tests`, then every remaining test project sequentially; from the repo root `openspec validate expand-flood --no-interactive`
