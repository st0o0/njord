## 1. Solar consolidation

- [x] 1.1 Move `Tilt` (30, 0..90) and `Azimuth` (180, 0..360) into `src/Njord.OpenMeteo/Configuration/SatelliteOptions.cs`; extend `SatelliteOptionsValidator.cs`; extend `src/Njord.OpenMeteo.Tests/Configuration/SatelliteOptionsValidatorTests.cs` (defaults, boundaries, out-of-range)
- [x] 1.2 Move `PeakKw` and `SystemEfficiency` into `src/Njord.Features/Conditions/SolarYield/SolarYieldConfig.cs`; create `SolarYieldConfigValidator.cs` beside it and register via `AddFeature<SolarYield, SolarYieldConfig, SolarYieldConfigValidator>()` in `src/Njord.Features/ServiceCollectionExtensions.cs`; add `SolarYieldConfigValidatorTests.cs` in `src/Njord.Features.Tests/Conditions/SolarYield/`
- [x] 1.3 Update `src/Njord.OpenMeteo/Forecast/ForecastClient.cs` and `src/Njord.OpenMeteo/Ensemble/EnsembleClient.cs` to read Tilt/Azimuth from `IOptions<SatelliteOptions>`; update `Njord.OpenMeteo.Tests` client tests (URL contains `tilt` and `azimuth=-45` for 135)
- [x] 1.4 Merge `SolarBatteryStrategy` into `src/Njord.Features/Conditions/SolarYield/SolarYield.cs`: add `BatteryCapacityKwh` (default 0) to `SolarYieldConfig`, move the `Decide` logic and battery result into `SolarYield.Evaluate` (emitted only when `BatteryCapacityKwh > 0`), use blended GTI (`Conditions/Shared/SolarIrradianceBlend.cs`) instead of ShortwaveRadiation; port battery tests into `SolarYieldTests.cs` (no battery, battery, GTI source, validator for negative capacity)
- [x] 1.5 Delete `src/Njord.Features/Conditions/SolarBatteryStrategy/`, `src/Njord.Features.Tests/Conditions/SolarBatteryStrategy/`, `src/Njord.Core/Configuration/SolarYieldOptions.cs`, `src/Njord.OpenMeteo/Configuration/SolarYieldOptionsValidator.cs`, `src/Njord.OpenMeteo.Tests/Configuration/SolarYieldOptionsValidatorTests.cs`; remove the `SolarYieldOptions` registration from `OpenMeteoSetupContainer` and the `SolarBatteryStrategy` line from `AddFeatures`; fix remaining references and architecture/integration tests
- [x] 1.6 Update docs, `AGENTS.md`, Config Builder (`docs/.vitepress/theme/builder`), `docs/data/features.json` (drop SolarBatteryStrategy, add `PeakKw`, `SystemEfficiency`, `BatteryCapacityKwh` to SolarYield) and `docker-compose.example.yml`/`appsettings.Example.json` for the `Njord:SolarYield` removal and `Njord:Satellite:Tilt/Azimuth`

### Validation 1

```powershell
cd src
dotnet build Njord.slnx
dotnet run --project Njord.OpenMeteo.Tests/Njord.OpenMeteo.Tests.csproj
dotnet run --project Njord.Features.Tests/Njord.Features.Tests.csproj
dotnet run --project Njord.Core.Tests/Njord.Core.Tests.csproj
dotnet run --project Njord.Architecture.Tests/Njord.Architecture.Tests.csproj
```

## 2. Alert threshold consistency

- [x] 2.1 `src/Njord.Features/Alerts/Uv/UvConfig.cs`: add `Thresholds` `[3, 6, 8, 11]`, `IHasArrayDefaults`, new `UvConfigValidator`; update the Uv alert to read thresholds from config; register with the validator; tests in `src/Njord.Features.Tests/Alerts/Uv/` (default levels unchanged, custom thresholds, validator)
- [x] 2.2 `src/Njord.Features/Alerts/Pm25/Pm25Config.cs`: `Thresholds` `[15, 25, 50]`, `IHasArrayDefaults`, `Pm25ConfigValidator`; update `Pm25Alert.cs`; tests
- [x] 2.3 `src/Njord.Features/Alerts/Pollen/PollenConfig.cs`: `Thresholds` `[20, 50, 100]`, `IHasArrayDefaults`, `PollenConfigValidator`; update `PollenAlert.cs`; tests
- [x] 2.4 `src/Njord.Features/Alerts/FloodRain/FloodRainConfig.cs`: add `HeavyRainThreshold` (30.0) and `ForecastDays` (7) with validation; update `FloodRainAlert.cs`; tests
- [x] 2.5 `src/Njord.Features/Alerts/FloodRisk/FloodRiskConfig.cs`: add `MedianFactor` (2.0) and `ForecastDays` (7) with validation; update `FloodRiskAlert.cs`; tests
- [x] 2.6 Create `src/Njord.Features/Alerts/Thunderstorm/ThunderstormConfigValidator.cs` for `CapeThresholds`; register in `AddFeatures`; tests in `src/Njord.Features.Tests/Alerts/Thunderstorm/`

### Validation 2

```powershell
cd src
dotnet build Njord.slnx
dotnet run --project Njord.Features.Tests/Njord.Features.Tests.csproj
```

## 3. WindowHours

- [x] 3.1 Add `WindowHours` (default 24, validated >= 1) to the configs under `src/Njord.Features/Alerts/` of: Frost, Heat, Humidity, Storm, Snow, WindChill, Ice, Visibility, TropicalNight, ClearSkyUv, Dust, WetBulbDanger, Uv, Pm25, Pollen, PressureDrop, Fog, HeavyRain; change each alert to `NextHoursInclusive(ctx.Now, config.Value.WindowHours)`; per alert add one test that a shorter window excludes a later hour, and extend validators (create where missing)
- [x] 3.2 Add the `WindowHours` option with default to each alert in `docs/data/features.json`

### Validation 3

```powershell
cd src
dotnet build Njord.slnx
dotnet run --project Njord.Features.Tests/Njord.Features.Tests.csproj
```

## 4. Cleanup

- [x] 4.1 Remove `IHasArrayDefaults` and the empty `ApplyDefaults` from `src/Njord.Core/Configuration/ForecastOptions.cs`; adjust `src/Njord.Core.Tests/Configuration/ForecastOptionsTests.cs` and `src/Njord.OpenMeteo.Tests/Configuration/ForecastOptionsValidatorTests.cs` (empty Models means best_match)
- [x] 4.2 Update `AGENTS.md`: clean config patterns (Tier 1-4 feature config, one validator per options class, `WindowHours` convention), solar section changes, options table (`Njord:Satellite` Tilt/Azimuth, no `Njord:SolarYield`), feature count
- [x] 4.3 Update the Config Builder (`docs/.vitepress/theme/builder/types.ts`, `serializer.ts`, `ConfigBuilder.vue`) for the new alert options, SolarYield battery option and Satellite Tilt/Azimuth

### Validation 4

```powershell
cd src
dotnet build Njord.slnx
dotnet run --project Njord.Core.Tests/Njord.Core.Tests.csproj
dotnet run --project Njord.OpenMeteo.Tests/Njord.OpenMeteo.Tests.csproj
```

## 5. Full test suite

- [x] 5.1 Run `dotnet format Njord.slnx` and all test projects sequentially

### Validation 5

```powershell
cd src
dotnet format Njord.slnx --verify-no-changes
dotnet run --project Njord.Messages.Tests/Njord.Messages.Tests.csproj
dotnet run --project Njord.Persistence.Tests/Njord.Persistence.Tests.csproj
dotnet run --project Njord.Core.Tests/Njord.Core.Tests.csproj
dotnet run --project Njord.OpenMeteo.Tests/Njord.OpenMeteo.Tests.csproj
dotnet run --project Njord.Budget.Tests/Njord.Budget.Tests.csproj
dotnet run --project Njord.Pipeline.Tests/Njord.Pipeline.Tests.csproj
dotnet run --project Njord.Polling.Tests/Njord.Polling.Tests.csproj
dotnet run --project Njord.Features.Tests/Njord.Features.Tests.csproj
dotnet run --project Njord.Grpc.Tests/Njord.Grpc.Tests.csproj
dotnet run --project Njord.IntegrationTests/Njord.IntegrationTests.csproj
dotnet run --project Njord.Architecture.Tests/Njord.Architecture.Tests.csproj
```
