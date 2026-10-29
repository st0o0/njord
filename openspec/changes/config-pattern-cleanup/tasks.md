## 1. Infrastructure: IHasArrayDefaults and registration helpers

- [x] 1.1 Add `IHasArrayDefaults` interface to `src/Njord.Core/Configuration/IHasArrayDefaults.cs` with `void ApplyDefaults()` method
- [x] 1.2 Update `AddNjordOptions<T>()` in `src/Njord.Core/Configuration/OptionsRegistrationExtensions.cs` to auto-register `PostConfigure<T>` when `T` implements `IHasArrayDefaults`
- [x] 1.3 Add `AddFeature<TRule, TConfig>()` and `AddFeature<TRule, TConfig, TValidator>()` extension methods in `src/Njord.Features/ServiceCollectionExtensions.cs`
- [x] 1.4 Test `AddNjordOptions` auto-detection in `src/Njord.Core.Tests/` with a fake config implementing `IHasArrayDefaults`: verify PostConfigure runs for array defaults, verify it does not run for non-implementing configs

### Validation
```
dotnet run --project src/Njord.Core.Tests/Njord.Core.Tests.csproj
```

## 2. Rename ForecastOptions to WeatherOptions

- [x] 2.1 Rename `src/Njord.Core/Configuration/ForecastOptions.cs` to `WeatherOptions.cs`; rename class to `WeatherOptions`; change `SectionName` from `"Njord:Forecast"` to `"Njord:Weather"`; implement `IHasArrayDefaults` for the `Models` array default
- [x] 2.2 Update `OpenMeteoOptionsValidator` in `src/Njord.OpenMeteo/Configuration/OpenMeteoOptionsValidator.cs`: change `IValidateOptions<ForecastOptions>` to `IValidateOptions<WeatherOptions>`, update all references
- [x] 2.3 Update `OpenMeteoSetupContainer` in `src/Njord.OpenMeteo/Configuration/OpenMeteoSetupContainer.cs`: change `AddNjordOptions<ForecastOptions, ...>` to `WeatherOptions`, remove the `PostConfigure<ForecastOptions>` lambda for Models (now handled by `IHasArrayDefaults`)
- [x] 2.4 Update all `IOptions<ForecastOptions>` consumers across the solution to `IOptions<WeatherOptions>` (grep for `ForecastOptions` in `*.cs` files)
- [x] 2.5 Update `WeatherModelCatalog` references if any use `ForecastOptions` by name
- [x] 2.6 Update tests in `src/Njord.OpenMeteo.Tests/` and `src/Njord.Core.Tests/` referencing `ForecastOptions`
- [x] 2.7 Fix `appsettings.Development.json` if section name reference needs updating (currently uses `"Weather"` which now matches)

### Validation
```
dotnet build src/Njord.slnx
dotnet run --project src/Njord.Core.Tests/Njord.Core.Tests.csproj
dotnet run --project src/Njord.OpenMeteo.Tests/Njord.OpenMeteo.Tests.csproj
```

## 3. Flatten feature config SectionNames (66 classes)

- [x] 3.1 Update all 30 alert config `SectionName` constants: remove category from path (e.g. `"Njord:Features:Forecast:Frost"` to `"Njord:Features:Frost"`) in `src/Njord.Features/Alerts/*/`
- [x] 3.2 Update all 7 score config `SectionName` constants in `src/Njord.Features/Scores/*/`
- [x] 3.3 Update all 25 condition config `SectionName` constants in `src/Njord.Features/Conditions/*/`
- [x] 3.4 Update the 1 trend config `SectionName` constant in `src/Njord.Features/Trends/*/`
- [x] 3.5 Update all 3 forecast config `SectionName` constants in `src/Njord.Features/Forecasts/*/`

### Validation
```
dotnet build src/Njord.slnx
```

## 4. Implement IHasArrayDefaults on feature configs

- [x] 4.1 Add `IHasArrayDefaults` implementation to all ~14 feature configs that currently use PostConfigure for array defaults: `DustConfig`, `FrostConfig`, `HeatConfig`, `HumidityConfig`, `RainProbabilityConfig`, `ShowerConfig`, `SnowConfig`, `StormConfig`, `ThunderstormConfig`, `TropicalNightConfig`, `VisibilityConfig`, `WetBulbDangerConfig`, `WindChillConfig`, `ClearSkyUvConfig`

### Validation
```
dotnet build src/Njord.slnx
```

## 5. Rewrite AddFeatures registration

- [x] 5.1 Replace all PostConfigure lambdas and multi-line registrations in `src/Njord.Features/ServiceCollectionExtensions.cs` with `AddFeature<TRule, TConfig>()` / `AddFeature<TRule, TConfig, TValidator>()` one-liners
- [x] 5.2 Remove the `PostConfigure<PollingOptions>` for `Budget` from `PollingSetupContainer` — keep this one, it is a derived value not an array default
- [x] 5.3 Verify `DomainSetupContainer` PostConfigure for `SolarYieldConfig` still works (it copies from `SolarYieldOptions`, not an array default)
- [ ] 5.4 Test feature registration: run `src/Njord.Features.Tests/` to verify all features resolve correctly

### Validation
```
dotnet run --project src/Njord.Features.Tests/Njord.Features.Tests.csproj
dotnet run --project src/Njord.Pipeline.Tests/Njord.Pipeline.Tests.csproj
```

## 6. Update Config Builder

- [x] 6.1 Update `docs/data/features.json`: change all `key` values from `"Category:Name"` to `"Name"` (e.g. `"Forecast:Frost"` to `"Frost"`); keep `category` field unchanged
- [x] 6.2 Update `docs/.vitepress/theme/builder/types.ts`: adapt `featureLabel`, `defaultFeatures`, `recommendedFeatures`, `userFeatureGroups` and any other code that splits feature keys on `:`
- [x] 6.3 Update `docs/.vitepress/theme/builder/serializer.ts`: remove `split(':')` from JSON export/import; flatten env var export/import paths; fix `n.Weather` references to match (already correct)
- [x] 6.4 Update `docs/.vitepress/theme/builder/budget.ts` if it references feature keys with `:` separator
- [ ] 6.5 Verify the Config Builder builds: `cd docs && npm run build`

### Validation
```
cd docs && npm run build
```

## 7. Update config files

- [ ] 7.1 Update `docker-compose.example.yml`: change `Njord__Forecast__` to `Njord__Weather__`; flatten all `Njord__Features__<Category>__<Name>__` to `Njord__Features__<Name>__`
- [ ] 7.2 Update `src/Njord/appsettings.Example.json`: flatten feature paths in comments; ensure `"Weather"` section naming is used
- [ ] 7.3 Verify `src/Njord/appsettings.Development.json` uses `"Weather"` (already does, now matches C#)

### Validation
```
dotnet build src/Njord.slnx
```

## 8. Update tests

- [ ] 8.1 Update any SectionName assertions in `src/Njord.Features.Tests/` that reference old category-prefixed paths
- [ ] 8.2 Update `src/Njord.Tests.Shared/AppSettingsDefaults.cs` if it references feature section names
- [ ] 8.3 Update `src/Njord.IntegrationTests/` if they reference config paths
- [ ] 8.4 Run all test projects to verify nothing is broken

### Validation
```
dotnet build src/Njord.slnx --no-incremental
dotnet run --project src/Njord.Core.Tests/Njord.Core.Tests.csproj --no-build
dotnet run --project src/Njord.Messages.Tests/Njord.Messages.Tests.csproj --no-build
dotnet run --project src/Njord.Persistence.Tests/Njord.Persistence.Tests.csproj --no-build
dotnet run --project src/Njord.OpenMeteo.Tests/Njord.OpenMeteo.Tests.csproj --no-build
dotnet run --project src/Njord.Budget.Tests/Njord.Budget.Tests.csproj --no-build
dotnet run --project src/Njord.Polling.Tests/Njord.Polling.Tests.csproj --no-build
dotnet run --project src/Njord.Pipeline.Tests/Njord.Pipeline.Tests.csproj --no-build
dotnet run --project src/Njord.Features.Tests/Njord.Features.Tests.csproj --no-build
dotnet run --project src/Njord.Grpc.Tests/Njord.Grpc.Tests.csproj --no-build
dotnet run --project src/Njord.IntegrationTests/Njord.IntegrationTests.csproj --no-build
dotnet run --project src/Njord.Architecture.Tests/Njord.Architecture.Tests.csproj --no-build
```

## 9. Update documentation

- [ ] 9.1 Rewrite the Options section in `AGENTS.md`: new pattern description with `IHasArrayDefaults`, `AddFeature` helper, three template variants (simple, scalar options, array thresholds); updated options table with `Njord:Weather` and `Njord:Features:<Name>`
- [ ] 9.2 Update the feature config guardrail in `AGENTS.md` (architecture guardrails section): change `Njord:Features:<Category>:<Name>` to `Njord:Features:<Name>`; mention `IHasArrayDefaults` and `AddFeature`
- [ ] 9.3 Update decisions section in `AGENTS.md` if it references `Njord:Forecast` or old feature paths
- [ ] 9.4 Update `CLAUDE.md` if it references config paths (check for `Njord:Features:` patterns)
- [ ] 9.5 Run `dotnet format src/Njord.slnx --verify-no-changes` to confirm formatting compliance

### Validation
```
dotnet format src/Njord.slnx --verify-no-changes
```
