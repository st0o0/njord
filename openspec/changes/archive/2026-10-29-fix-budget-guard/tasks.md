## 1. CallWeight and DomainCallConfig

- [x] 1.1 Change `CallWeight.Of()` in `src/Njord.Core/Polling/CallWeight.cs` to return `double` with fractional day multiplier: `Math.Ceiling(variableCount / 10.0) * (forecastDays / 14.0)`. Update `src/Njord.Core.Tests/` with `CallWeightTests` covering short periods (4d), long periods (92d), exact 14d boundary, and >10 variables.
- [x] 1.2 Create `DomainCallConfig` record in `src/Njord.Core/Polling/DomainCallConfig.cs` with properties: `ForecastDays`, `ForecastModelCount`, `EnsembleDays`, `EnsembleModelCount`, `AirQualityDays`, `FloodDays`, `FloodIncludeEnsemble`, `MarineDays`, `SatellitePastHours`, `SeasonalMonths`.

### Validation

```
dotnet run --project Njord.Core.Tests/Njord.Core.Tests.csproj
```

## 2. Move Options to OpenMeteo

- [x] 2.1 Move `ForecastOptions.cs`, `ForecastOptionsValidator.cs` from `src/Njord.Core/Configuration/` to `src/Njord.OpenMeteo/Configuration/`. Change namespace to `Njord.OpenMeteo.Configuration`. Update `using` in all consumers: `src/Njord.OpenMeteo/Forecast/ForecastClient.cs`, `src/Njord/Configuration/` setup containers, `src/Njord.Core.Tests/Configuration/ForecastOptionsValidatorTests.cs` (move to `src/Njord.OpenMeteo.Tests/Configuration/`).
- [x] 2.2 Move `EnsembleOptions.cs`, `EnsembleOptionsValidator.cs` from `src/Njord.Core/Configuration/` to `src/Njord.OpenMeteo/Configuration/`. Update `using` in: `src/Njord.OpenMeteo/Ensemble/EnsembleClient.cs`, `src/Njord.Grpc/WeatherGrpcService.cs` (add temporary `ProjectReference` to OpenMeteo), `src/Njord/Configuration/` setup containers, `src/Njord.Core.Tests/Configuration/EnsembleOptionsValidatorTests.cs` and `EnsembleOptionsTests.cs` (move to `src/Njord.OpenMeteo.Tests/Configuration/`).
- [x] 2.3 Move `FloodOptions.cs`, `FloodOptionsValidator.cs` from `src/Njord.Core/Configuration/` to `src/Njord.OpenMeteo/Configuration/`. Update `using` in: `src/Njord.OpenMeteo/Flood/FloodClient.cs`, `src/Njord/Configuration/` setup containers, `src/Njord.Core.Tests/Configuration/FloodOptionsValidatorTests.cs` (move to `src/Njord.OpenMeteo.Tests/Configuration/`).
- [x] 2.4 Add temporary architecture test exception in `src/Njord.Architecture.Tests/ArchitectureTests.cs` for `Njord.Grpc` → `Njord.OpenMeteo` reference, documented as tech debt for the Catalog change.

### Validation

```
dotnet run --project Njord.Core.Tests/Njord.Core.Tests.csproj
dotnet run --project Njord.OpenMeteo.Tests/Njord.OpenMeteo.Tests.csproj
dotnet run --project Njord.Grpc.Tests/Njord.Grpc.Tests.csproj
dotnet run --project Njord.Architecture.Tests/Njord.Architecture.Tests.csproj
```

## 3. BudgetGuard rewrite

- [x] 3.1 Register `DomainCallConfig` in the Host: add a `PostConfigure` or factory in `src/Njord/Configuration/` that builds `DomainCallConfig` from `ForecastOptions`, `EnsembleOptions`, `FloodOptions`, `AirQualityOptions`, `MarineOptions`, `SatelliteOptions`, `SeasonalOptions` and registers it as `IOptions<DomainCallConfig>`.
- [x] 3.2 Rewrite `src/Njord.Polling/Configuration/BudgetGuard.cs` to accept `DomainCallConfig` + `DomainRequirements` instead of individual Options. Compute `CallWeight.Of(PopCount(requirements.All<Domain>), callConfig.<Domain>Days)` for every required domain. Apply model multipliers for Forecast and Ensemble. Handle Satellite via `PastHours / 336.0` denominator. Keep Seasonal as `SeasonalCallsPerMonth` weighted by `ceil(vars/10) × (months × 30 / 14.0)` or 1.3 per request, whichever is higher.
- [x] 3.3 Update `src/Njord.Polling/Configuration/PollingOptionsValidator.cs` to inject `IOptions<DomainCallConfig>` instead of `IOptions<ForecastOptions>`, `IOptions<EnsembleOptions>`, `IOptions<FloodOptions>`. Remove the OpenMeteo/Core Options imports.
- [x] 3.4 Update `src/Njord.Polling.Tests/BudgetGuardTests.cs` with tests for: Flood weight (92d ≈ 6.57), AirQuality weight (5d, many vars), Marine weight (8d), Satellite weight (PastHours-based), total projection accuracy, and the over-budget rejection threshold. Update `src/Njord.Polling.Tests/` for PollingOptionsValidator changes.

### Validation

```
dotnet run --project Njord.Polling.Tests/Njord.Polling.Tests.csproj
dotnet run --project Njord.IntegrationTests/Njord.IntegrationTests.csproj
```

## 4. Full validation

- [x] 4.1 Run all test projects, `dotnet format`, and verify the build is clean. Update `AGENTS.md` and `CLAUDE.md` if any documented BudgetGuard behavior or Options locations changed.

### Validation

```
dotnet build Njord.slnx
dotnet format Njord.slnx --verify-no-changes
for p in Njord.*Tests; do [ "$p" = Njord.Tests.Shared ] && continue; dotnet run --project "$p/$p.csproj" --no-build; done
```
