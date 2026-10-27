## 1. Forecast Domain Types (Njord.Forecast)

- [x] 1.1 Add `DomainKind.Ensemble` to the `DomainKind` enum in `src/Njord.Forecast/DomainKind.cs`
- [x] 1.2 Add `EnsemblePoint` record implementing `IHasValidTime` with `ValidFrom`, `TemperatureSpread`, `PrecipitationSpread`, `WindSpeedSpread` (all `double?`) in `src/Njord.Forecast/Weather/EnsemblePoint.cs`. Add a `SpreadAt(DateTimeOffset validFrom)` static helper on a companion extensions class for timestamp lookup across a forecast's points
- [x] 1.3 Add `EnsembleParams` flags enum (`TemperatureSpread`, `PrecipitationSpread`, `WindSpeedSpread`, `Models`) in `src/Njord.Forecast/Weather/EnsembleParams.cs`
- [x] 1.4 Remove `EnsembleStdDev`, `PrecipitationSpread`, `WindSpeedSpread` from `WeatherPoint` in `src/Njord.Forecast/Weather/WeatherPoint.cs`. Remove corresponding `HasFields` cases and `WeatherParams` flags
- [x] 1.5 Tests: `EnsemblePointTests` in `src/Njord.Forecast.Tests/Weather/EnsemblePointTests.cs`, `EnsembleParamsTests` in `src/Njord.Forecast.Tests/Weather/EnsembleParamsTests.cs`. Update `WeatherPointTests` for removed fields

### Validation
```
dotnet run --project Njord.Forecast.Tests/Njord.Forecast.Tests.csproj
```

## 2. Core Domain Infrastructure (Njord.Core)

- [x] 2.1 Add `Ensemble: EnsembleParams` slot to `DomainRequirements` in `src/Njord.Core/Domain/DomainRequirements.cs`. Update the union operator and `RequiresDomain` to handle Ensemble
- [x] 2.2 Add `DomainForecast<EnsemblePoint>? Ensemble` property to `LocationForecast` in `src/Njord.Core/Domain/LocationForecast.cs`
- [x] 2.3 Add `WeatherEnsembleInput` typed input record in `src/Njord.Core/Domain/FeatureInputs.cs` containing `DomainForecast<WeatherPoint>` and `DomainForecast<EnsemblePoint>`
- [x] 2.4 Update `FeatureResolution` in `src/Njord.Core/Domain/FeatureResolution.cs` to resolve ensemble requirements (spread variables, model list). Add ensemble to `FeatureRequirementsOptions`
- [x] 2.5 Update `BudgetGuard` in `src/Njord.Polling/Configuration/BudgetGuard.cs` to project ensemble call weight (spread-only: 1 call; with models: ceil(vars/10) * models)
- [x] 2.6 Tests: update `DomainRequirementsTests` in `src/Njord.Core.Tests/Domain/`, add `EnsembleRequirementsTests`. Update `FeatureResolutionTests`, `LocationForecastTests`, `BudgetGuardTests`

### Validation
```
dotnet run --project Njord.Core.Tests/Njord.Core.Tests.csproj
dotnet run --project Njord.Polling.Tests/Njord.Polling.Tests.csproj
```

## 3. OpenMeteo Clients (Njord.OpenMeteo)

- [x] 3.1 Update `OpenMeteoEndpoints` in `src/Njord.OpenMeteo/Http/OpenMeteoEndpoints.cs`: rename `WeatherHost`/`WeatherPath` to `EnsembleHost`/`EnsemblePath`, add `ForecastHost` (`https://api.open-meteo.com/`) and `ForecastPath` (`v1/forecast`)
- [x] 3.2 Create `ForecastClient : IDomainClient` in `src/Njord.OpenMeteo/Weather/ForecastClient.cs`. `Domain = Weather`, fetches from `ForecastPath`, no `models` parameter, `CallWeight = ceil(vars/10) * ceil(days/14)`. Reuses `WeatherResponse` mapping
- [x] 3.3 Create `EnsembleClient : IDomainClient` in `src/Njord.OpenMeteo/Ensemble/EnsembleClient.cs`. `Domain = Ensemble`, fetches from `EnsemblePath` with `models` parameter. Primary output: `EnsemblePoint` from spread fields. Secondary: per-model `WeatherPoint` in `FetchSuccess.Models`. Create `EnsembleResponseMappingExtensions` for spread extraction
- [x] 3.4 Add `EnsembleApiVariable` enum and `EnsembleApiVariableMappingExtensions` in `src/Njord.OpenMeteo/Ensemble/` for the spread variable names (`temperature_2m_spread`, `precipitation_spread`, `wind_speed_10m_spread`)
- [x] 3.5 Remove `WeatherClient.cs` from `src/Njord.OpenMeteo/Weather/`
- [x] 3.6 Remove `EnsembleStdDev`, `PrecipitationSpread`, `WindSpeedSpread` from `WeatherHourlyData` in `src/Njord.OpenMeteo/Weather/WeatherResponse.cs` and from `WeatherApiVariable` enum. Remove from `WeatherResponseMappingExtensions` point construction
- [x] 3.7 Update `OpenMeteoSetupContainer` in `src/Njord.OpenMeteo/Configuration/OpenMeteoSetupContainer.cs`: replace `AddDomainClient<WeatherClient>(WeatherHost)` with `AddDomainClient<ForecastClient>(ForecastHost)` and `AddDomainClient<EnsembleClient>(EnsembleHost)`
- [x] 3.8 Tests: `ForecastClientTests` and `EnsembleClientTests` in `src/Njord.OpenMeteo.Tests/`. Update `OpenMeteoClientTests` for URL assertions. Update `WeatherModelResponseTests`. Add `EnsembleResponseMappingTests`

### Validation
```
dotnet run --project Njord.OpenMeteo.Tests/Njord.OpenMeteo.Tests.csproj
```

## 4. Feature Rules (Njord.Features)

- [x] 4.1 Update 9 alerts to use `WeatherEnsembleInput` and require `Ensemble: EnsembleParams.TemperatureSpread`: Frost, Heat, Storm, Thunderstorm, Snow, Ice, HeavyRain, WindChill, TropicalNight in `src/Njord.Features/Alerts/`. Change `Project` to extract ensemble from `LocationForecast.Ensemble`. Change confidence to read from `EnsemblePoint.SpreadAt(point.ValidFrom)`
- [x] 4.2 Update 5 alerts to use `WeatherInput` (no Ensemble) with `ConfidenceLevel.Medium`: PressureDrop, Fog, Visibility, UV, Humidity. Remove `EnsembleStdDev` from their `DomainRequirements`
- [x] 4.3 Update `ConfidenceTrend` in `src/Njord.Features/Trends/ConfidenceTrend/` to require `Ensemble: EnsembleParams.TemperatureSpread` and read from `EnsemblePoint`
- [x] 4.4 Update `ModelComparison` in `src/Njord.Features/Forecasts/ModelComparison/` to require `Ensemble: EnsembleParams.Models` and read comparison models from `LocationForecast.Ensemble`'s associated `Models`
- [x] 4.5 Update `AddFeatures` registration in `src/Njord.Features/ServiceCollectionExtensions.cs` if any registration changes needed
- [x] 4.6 Tests: update all affected alert tests in `src/Njord.Features.Tests/Alerts/`. Update `ConfidenceTrendTests`, `ModelComparisonTests`. Verify Medium confidence for alerts without Ensemble

### Validation
```
dotnet run --project Njord.Features.Tests/Njord.Features.Tests.csproj
```

## 5. Persistence Records (Njord.Persistence)

- [x] 5.1 Remove spread fields from any weather-related persistence records in `src/Njord.Persistence/`. Add new members as nullable/defaulted per extend-only convention. Update `PersistedDomainKind` to include `Ensemble`
- [x] 5.2 Update persistence mapping extensions for the weather/ensemble split
- [x] 5.3 Tests: update snapshot tests in `src/Njord.Persistence.Tests/`

### Validation
```
dotnet run --project Njord.Persistence.Tests/Njord.Persistence.Tests.csproj
```

## 6. Pipeline and Polling (Njord.Pipeline, Njord.Polling)

- [x] 6.1 Update `FeatureEngine` in `src/Njord.Pipeline/Stream/FeatureEngine.cs` to populate `LocationForecast.Ensemble` from `DomainDataReceived` with `DomainKind.Ensemble`
- [x] 6.2 Update `PollWorker` mapping in `src/Njord.Polling/` to handle `DomainKind.Ensemble` (new domain timer, fetch dispatch)
- [x] 6.3 Update `ShardMessageExtractor` if needed for ensemble domain routing
- [x] 6.4 Tests: update `FeatureEngineTests` in `src/Njord.Pipeline.Tests/`. Update poll worker tests in `src/Njord.Polling.Tests/`

### Validation
```
dotnet run --project Njord.Pipeline.Tests/Njord.Pipeline.Tests.csproj
dotnet run --project Njord.Polling.Tests/Njord.Polling.Tests.csproj
```

## 7. gRPC and Proto (Njord.Grpc, protos/)

- [x] 7.1 Update `protos/njord/v2/weather.proto`: remove spread fields from weather sample, add ensemble domain data, update `DomainKind` enum
- [x] 7.2 Update `DomainDataProtoMappingExtensions` in `src/Njord.Grpc/Mapping/` for ensemble domain mapping
- [x] 7.3 Update `CatalogMessageMappingExtensions` to list ensemble as a domain in GetCatalog
- [x] 7.4 Tests: update `src/Njord.Grpc.Tests/` for mapping changes

### Validation
```
dotnet run --project Njord.Grpc.Tests/Njord.Grpc.Tests.csproj
```

## 8. Host, Config, and Documentation

- [x] 8.1 Update `WeatherModelCatalog` in `src/Njord.Core/Domain/WeatherModelCatalog.cs` to distinguish forecast models from ensemble-mean models (add an `Endpoint` or `Kind` field)
- [x] 8.2 Update `OpenMeteoOptionsValidator` in `src/Njord.OpenMeteo/Configuration/` for the new model validation (ensemble models go to EnsembleClient, forecast uses best_match)
- [x] 8.3 Update `src/Njord.Tests.Shared/` fakes: replace `FakeWeatherClient` with `FakeForecastClient`, add `FakeEnsembleClient`
- [x] 8.4 Update integration tests in `src/Njord.IntegrationTests/` for the new client registrations
- [x] 8.5 Update architecture tests in `src/Njord.Architecture.Tests/` if zone rules need Ensemble references

### Validation
```
dotnet run --project Njord.Core.Tests/Njord.Core.Tests.csproj
dotnet run --project Njord.IntegrationTests/Njord.IntegrationTests.csproj
dotnet run --project Njord.Architecture.Tests/Njord.Architecture.Tests.csproj
```

## 9. E2E and Config Builder

- [x] 9.1 Update `e2e/docker-compose.e2e.yml`: weather configuration reflects forecast-only primary, ensemble models configured separately
- [x] 9.2 Update `docs/data/features.json` with ensemble domain requirements per feature
- [x] 9.3 Update Config Builder (`docs/.vitepress/theme/builder/`) for ensemble domain: `budget.ts` call weight formula, `types.ts` domain list, `ConfigBuilder.vue` UI
- [x] 9.4 Update `docker-compose.example.yml` at repo root for the new configuration layout
- [x] 9.5 Update `AGENTS.md` and `CLAUDE.md` domain lists, architecture guardrails, and decisions sections

### Validation
```
dotnet build Njord.slnx
dotnet format Njord.slnx --verify-no-changes
```

Full test suite:
```bash
for p in Njord.*Tests; do
  [ "$p" = Njord.Tests.Shared ] && continue
  dotnet run --project "$p/$p.csproj" --no-build
done
```
