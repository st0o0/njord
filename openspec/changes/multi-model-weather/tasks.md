## 1. Config and catalog
- [ ] 1.1 Measure the real call weighting of a multi-model request against the live API; confirm or adjust the `x modelCount` multiplier in proposal and design
- [ ] 1.2 Tests first (`src/Njord.Core.Tests`), then `Models` in `src/Njord.Core/Configuration/WeatherOptions.cs` (remove `Model`) and rules in `WeatherOptionsValidator.cs`
- [ ] 1.3 Add `src/Njord.Core/Domain/WeatherModelCatalog.cs` (verify every id against the API) with a spec pinning each entry
- [ ] 1.4 Add the `ModelComparison` feature (`OutputKind.Forecast`, default off) and effective-model resolution; `BudgetCalculator` multiplies the weather weight (`src/Njord.Core/Configuration/Polling/BudgetCalculator.cs`)

## 2. Client
- [ ] 2.1 `src/Njord.OpenMeteo/WeatherClient.cs` sends comma-separated `models`; extend the WeatherClient URL spec (no RequestBuilder after `rewrite-gateway`)
- [ ] 2.2 `WeatherResponse.cs` / `OpenMeteoJsonContext.cs`: per-model hourly map for suffixed and unsuffixed shapes (fixtures for 1 and 3 models)

## 3. Response mapping
- [ ] 3.1 `Mapping/WeatherResponseMappings.cs` returns a forecast per model; empty model omitted; one spec per scenario
- [ ] 3.2 thick `WeatherClient` (`IDomainClient`) returns all models and the thin `OpenMeteoGateway` forwards them through the SinkRef branch, one `DomainDataReceived` per model; fetch outcome contract unchanged

## 4. Pipeline and forecast container
- [ ] 4.1 `ModelId` on `DomainDataReceived`; `LocationForecast.ModelWeather` in `src/Njord.Core/Domain/LocationForecast.cs`
- [ ] 4.2 `ProjectionEngine` merges per model and runs enrichments for the primary only (`src/Njord.Pipeline`); `NjordActorSpec` specs
- [ ] 4.3 Model id on `QueryDomainData` / `DomainDataNotAvailable` (`src/Njord.Messages/Forecast`)

## 5. gRPC
- [ ] 5.1 `protos/njord/v2/weather.proto`: `model` on request and update, `ModelInfo.enabled`; follow `proto-sync-workflow`
- [ ] 5.2 `WeatherGrpcService` and `Mapping/` serve per-model data; `NOT_FOUND` for unknown models; update `WeatherGrpcServiceSpec` and wire-format golden masters

## 6. Docs, builder and ha-njord notes
- [ ] 6.1 Config Builder model section and per-model cost (`docs/.vitepress/theme/builder/types.ts`, `budget.ts`, `serializer.ts`, `ConfigBuilder.vue`, `docs/data/`)
- [ ] 6.2 `docker-compose.example.yml`, `src/Njord/appsettings.Example.json`, AGENTS.md options table, VitePress model-comparison page
- [ ] 6.3 ha-njord contract notes (entity ids, attributes, primary-model default) handed over for a separate ha-njord change; integration test in `src/Njord.IntegrationTests` with two models
