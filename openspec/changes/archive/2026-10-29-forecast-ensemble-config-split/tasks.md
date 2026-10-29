## 1. New Options Classes + Validators

- [x] 1.1 Create `ForecastOptions` in `src/Njord.Core/Configuration/ForecastOptions.cs`: `IConfigSection` with `SectionName = "Njord:Forecast"`, `ForecastDays` (4, int), `PastHours` (0, int), `Models` (string[], `IHasArrayDefaults`, default empty). Tests in `src/Njord.Core.Tests/Configuration/ForecastOptionsTests.cs`.
- [x] 1.2 Create `EnsembleOptions` in `src/Njord.Core/Configuration/EnsembleOptions.cs`: `IConfigSection` with `SectionName = "Njord:Ensemble"`, `ForecastDays` (4, int), `Models` (string[], `IHasArrayDefaults`, default `[ecmwf_ifs025_ensemble_mean]`). `DefaultModel` constant. Tests in `src/Njord.Core.Tests/Configuration/EnsembleOptionsTests.cs`.
- [x] 1.3 Create `ForecastOptionsValidator` in `src/Njord.OpenMeteo/Configuration/ForecastOptionsValidator.cs`: ForecastDays 1..16, PastHours 0..96, Models no duplicates. Tests in `src/Njord.OpenMeteo.Tests/Configuration/ForecastOptionsValidatorTests.cs`.
- [x] 1.4 Create `EnsembleOptionsValidator` in `src/Njord.OpenMeteo/Configuration/EnsembleOptionsValidator.cs`: ForecastDays 1..35, Models non-empty + no duplicates. Tests in `src/Njord.OpenMeteo.Tests/Configuration/EnsembleOptionsValidatorTests.cs`.
- [x] 1.5 Register both options in `src/Njord.OpenMeteo/Configuration/OpenMeteoSetupContainer.cs` via `AddNjordOptions<T, TValidator>()`. Remove `WeatherOptions` registration from `OpenMeteoSetupContainer`.

### Validation

```
dotnet run --project Njord.Core.Tests/Njord.Core.Tests.csproj
dotnet run --project Njord.OpenMeteo.Tests/Njord.OpenMeteo.Tests.csproj
```

## 2. DomainRequirements: struct to class + Enhancement + Registration

- [x] 2.1 Change `DomainRequirements` in `src/Njord.Core/Domain/DomainRequirements.cs` from `readonly record struct` to `sealed record` (class). Add parameterless constructor. Replace `default(DomainRequirements)` with `new DomainRequirements()` across all call sites (~5 in `src/Njord.Core.Tests/`, `src/Njord.Messages/`, `src/Njord.Pipeline.Tests/`). Verify the `|` operator, `with`-expressions, and positional constructor still work. Run `src/Njord.Core.Tests/Domain/DomainRequirementsTests.cs` to confirm.
- [x] 2.2 Add `WithBaseParams()` instance method to `DomainRequirements`. Move logic from `FeatureResolution.WithBaseParams()`. Tests in `src/Njord.Core.Tests/Domain/DomainRequirementsTests.cs`.
- [x] 2.3 Add `RequiredDomains()` extension method in `src/Njord.Core/Domain/DomainRequirementsExtensions.cs`. Tests in `src/Njord.Core.Tests/Domain/DomainRequirementsExtensionsTests.cs`.
- [x] 2.4 Register `DomainRequirements` as `IOptions<DomainRequirements>` via `AddOptions<DomainRequirements>().Configure<IEnumerable<IFeatureRule>>(...)` with inline OR + `WithBaseParams()` in `src/Njord/Configuration/DomainSetupContainer.cs`. Add `ValidateOnStart()`. Tests in `src/Njord.Core.Tests/Domain/DomainRequirementsResolutionTests.cs` (port from existing `FeatureResolutionConfigureTests`).

### Validation

```
dotnet run --project Njord.Core.Tests/Njord.Core.Tests.csproj
```

## 3. Migrate Consumers

- [x] 3.1 Update `ForecastClient` (`src/Njord.OpenMeteo/Forecast/ForecastClient.cs`): replace `IOptions<WeatherOptions>` with `IOptions<ForecastOptions>`, replace `IOptions<FeatureRequirementsOptions>` with `IOptions<DomainRequirements>`. Add optional multi-model: when `ForecastOptions.Models` is non-empty, send `models=` parameter and use `SplitByModel` for per-model responses. Update tests in `src/Njord.OpenMeteo.Tests/Forecast/ForecastClientTests.cs`.
- [x] 3.2 Update `EnsembleClient` (`src/Njord.OpenMeteo/Ensemble/EnsembleClient.cs`): replace `IOptions<WeatherOptions>` with `IOptions<EnsembleOptions>`, replace `IOptions<FeatureRequirementsOptions>` with `IOptions<DomainRequirements>`. Read models from `EnsembleOptions.Models`, decide multi-model from `EnsembleParams.Models` flag. Remove `ComparisonFeatureName` dependency. Update tests in `src/Njord.OpenMeteo.Tests/Ensemble/EnsembleClientTests.cs`.
- [x] 3.3 Update remaining domain clients (`AirQualityClient`, `FloodClient`, `MarineClient`, `SeasonalClient`, `SatelliteRadiationClient`): replace `IOptions<FeatureRequirementsOptions>` with `IOptions<DomainRequirements>`. Update tests in `src/Njord.OpenMeteo.Tests/`.
- [x] 3.4 Update `PollWorker` (`src/Njord.Polling/PollWorker.cs`): replace `IOptions<FeatureRequirementsOptions>` with `IOptions<DomainRequirements>`, use `.Value.RequiredDomains()`. Update tests in `src/Njord.Polling.Tests/PollWorker*Tests.cs`.
- [x] 3.5 Update `FeatureEngine` (`src/Njord.Pipeline/Stream/FeatureEngine.cs`): inline `features.Where(f => f.Enabled)` instead of `FeatureResolution.EnabledFeatures()`. Remove `Requirements` property. Update `src/Njord.Pipeline.Tests/Stream/FeatureEngineTests.cs`.

### Validation

```
dotnet run --project Njord.OpenMeteo.Tests/Njord.OpenMeteo.Tests.csproj
dotnet run --project Njord.Polling.Tests/Njord.Polling.Tests.csproj
dotnet run --project Njord.Pipeline.Tests/Njord.Pipeline.Tests.csproj
```

## 4. BudgetGuard + PollingOptionsValidator

- [x] 4.1 Update `BudgetGuard` (`src/Njord.Polling/Configuration/BudgetGuard.cs`): accept `ForecastOptions` + `EnsembleOptions` instead of `WeatherOptions`. Use `ForecastOptions.ForecastDays` for forecast weight, `EnsembleOptions.ForecastDays` for ensemble weight, `EnsembleOptions.Models` for model count (filtered by `EnsembleParams.Models` flag from `IOptions<DomainRequirements>`). Account for `ForecastOptions.Models.Length` when non-empty. Update tests in `src/Njord.Polling.Tests/BudgetGuardTests.cs`.
- [x] 4.2 Update `PollingOptionsValidator` (`src/Njord.Polling/Configuration/PollingOptionsValidator.cs`): replace raw `IConfiguration` access with `IOptions<ForecastOptions>`, `IOptions<EnsembleOptions>`, `IOptions<NjordOptions>`, `IOptions<FloodOptions>`. Remove the comment about reading from configuration. Update tests in `src/Njord.Polling.Tests/Configuration/PollingOptionsValidatorTests.cs`.

### Validation

```
dotnet run --project Njord.Polling.Tests/Njord.Polling.Tests.csproj
```

## 5. gRPC + Catalog

- [x] 5.1 Update `WeatherGrpcService` (`src/Njord.Grpc/WeatherGrpcService.cs`): replace `IOptions<FeatureRequirementsOptions>` with `IOptions<EnsembleOptions>` for model list access. Update tests in `src/Njord.Grpc.Tests/`.
- [x] 5.2 Update `CatalogMessageMappingExtensions` (`src/Njord.Grpc/Mapping/CatalogMessageMappingExtensions.cs`): read model list from `EnsembleOptions.Models` instead of `WeatherModelCatalog.All`. Remove `WeatherModelInfo` mapping. Update tests.

### Validation

```
dotnet run --project Njord.Grpc.Tests/Njord.Grpc.Tests.csproj
```

## 6. Remove Old Code

- [x] 6.1 Delete `WeatherOptions` (`src/Njord.Core/Configuration/WeatherOptions.cs`) and its tests.
- [x] 6.2 Delete `WeatherModelCatalog` (`src/Njord.Core/Domain/WeatherModelCatalog.cs`), `WeatherModelInfo`, and their tests.
- [x] 6.3 Delete `FeatureRequirementsOptions` (`src/Njord.Core/Domain/FeatureRequirementsOptions.cs`) and its tests.
- [x] 6.4 Delete `FeatureResolution` (`src/Njord.Messages/Results/FeatureResolution.cs`) and its tests (`src/Njord.Core.Tests/Domain/FeatureResolutionTests.cs`, `FeatureResolutionConfigureTests.cs`).
- [x] 6.5 Remove `ComparisonFeatureName` from `ModelComparison` (`src/Njord.Features/Forecasts/ModelComparison/ModelComparison.cs`) if it still has the constant.
- [x] 6.6 Remove `WeatherOptions` validation from `OpenMeteoOptionsValidator` (`src/Njord.OpenMeteo/Configuration/OpenMeteoOptionsValidator.cs`): remove the `IValidateOptions<WeatherOptions>` implementation. Update tests.

### Validation

```
dotnet build Njord.slnx
dotnet run --project Njord.Core.Tests/Njord.Core.Tests.csproj
dotnet run --project Njord.Messages.Tests/Njord.Messages.Tests.csproj
dotnet run --project Njord.OpenMeteo.Tests/Njord.OpenMeteo.Tests.csproj
dotnet run --project Njord.Features.Tests/Njord.Features.Tests.csproj
```

## 7. Integration + Host Wiring

- [x] 7.1 Update `DomainSetupContainer` (`src/Njord/Configuration/DomainSetupContainer.cs`): remove `FeatureRequirementsOptions` registration, add `DomainRequirements` registration with `AddOptions<DomainRequirements>().Configure<IEnumerable<IFeatureRule>>(...)` inline delegate.
- [x] 7.2 Update integration tests (`src/Njord.IntegrationTests/`): replace `FeatureRequirementsOptions` usage with `IOptions<DomainRequirements>` and per-API options.
- [x] 7.3 Run architecture tests to verify zone rules still pass.

### Validation

```
dotnet run --project Njord.IntegrationTests/Njord.IntegrationTests.csproj
dotnet run --project Njord.Architecture.Tests/Njord.Architecture.Tests.csproj
```

## 8. Config Files + Documentation

- [x] 8.1 Update `src/Njord/appsettings.Example.json`: replace `Njord:Weather` with `Njord:Forecast` and `Njord:Ensemble` sections.
- [x] 8.2 Update `docker-compose.example.yml` (repo root): replace `Njord__Weather__*` env vars with `Njord__Forecast__*` and `Njord__Ensemble__*`.
- [x] 8.3 Update Config Builder (`docs/.vitepress/theme/builder/`): `types.ts`, `budget.ts`, `serializer.ts`, `ConfigBuilder.vue` for the two new sections.
- [x] 8.4 Update `AGENTS.md` and `CLAUDE.md`: Options table, Decisions section, WeatherOptions references.

### Validation

```
dotnet format Njord.slnx --verify-no-changes
```

## 9. Full Test Suite

- [x] 9.1 Run all test projects sequentially to confirm no regressions.

### Validation

```bash
for p in Njord.*Tests; do
  [ "$p" = Njord.Tests.Shared ] && continue
  dotnet run --project "$p/$p.csproj" --no-build
done
```
