All test projects run sequentially from `src/`: `dotnet run --project <Project>.Tests/<Project>.Tests.csproj`. Implementation is TDD; xUnit v3, `Spec` suffix, sealed classes, `TestContext.Current.CancellationToken`, `FakeTimeProvider`. Run `dotnet format Njord.slnx` after editing `.cs` files.

## 1. Seasonal domain model

- [x] 1.1 Verify the Open-Meteo Seasonal API (variables, units, response shape); record the outcome in design D3
- [x] 1.2 Add `DomainKind.Seasonal`, `TimeResolution.Monthly`, `SeasonalParams`, `SeasonalPoint` in `src/Njord.Forecast`
- [x] 1.3 Add `Seasonal`/`OptionalSeasonal` to `DomainRequirements`, `Seasonal` to `LocationForecast`
- [x] 1.4 `SeasonalOptions` + validator (`Njord:Seasonal:ForecastMonths`)
- [x] 1.5 `BudgetCalculator` counts Seasonal as 30 calls per month and location

## 2. Gateway client

- [x] 2.1 `SeasonalResponse`, `ISeasonalClient`, `SeasonalClient`, `OpenMeteoRequestBuilder.WithMonthly`, JSON context
- [x] 2.2 `SeasonalParamsMappings` and `SeasonalResponseMappings.ToDomainForecast`
- [x] 2.3 Register the typed client and options; dispatch `DomainKind.Seasonal` in `OpenMeteoGateway`
- [x] 2.4 Tests: URL, parsing, mapping, gateway routing

## 3. Pipeline, handler, wire

- [x] 3.1 `PipelineEvent.SeasonalDataReceived`, `PipelineEventMappings`, telemetry tags, proto `DOMAIN_SEASONAL`
- [x] 3.2 `MonthlyOutlook`, `OutlookConfidence`, `PipelineEvent.SeasonalOutlookComputed`, `LifestyleKind.SeasonalOutlook` with wire mapping
- [x] 3.3 `FeatureKind.SeasonalOutlook` + `SeasonalOutlookHandler` (registered in `AddWeatherDomain`)
- [x] 3.4 Tests: handler, wire format, feature key

## 4. Config Builder + Docs

- [x] 4.1 `docs/data/features.json`, builder budget, `docker-compose.example.yml`, configuration docs, `AGENTS.md`

## 5. ha-njord

Separate repository, tracked as a follow-up: seasonal outlook sensor, `SeasonalOutlook` toggle in the config flow, `DOMAIN_SEASONAL` enum value.

## 6. Validation

- [x] 6.1 `dotnet build Njord.slnx` and `dotnet format Njord.slnx --verify-no-changes`
- [x] 6.2 All test projects pass sequentially
- [x] 6.3 `openspec validate --all --no-interactive` (repo root)
