## 1. Split God Validator

- [x] 1.1 Create `AirQualityOptionsValidator` in `src/Njord.OpenMeteo/Configuration/AirQualityOptionsValidator.cs`: extract the `IValidateOptions<AirQualityOptions>` implementation from `OpenMeteoOptionsValidator`. Register via `AddNjordOptions<AirQualityOptions, AirQualityOptionsValidator>()` in `OpenMeteoSetupContainer`. Tests in `src/Njord.OpenMeteo.Tests/Configuration/AirQualityOptionsValidatorTests.cs`.
- [x] 1.2 Create `FloodOptionsValidator` in `src/Njord.OpenMeteo/Configuration/FloodOptionsValidator.cs`: extract `IValidateOptions<FloodOptions>`. Tests in `src/Njord.OpenMeteo.Tests/Configuration/FloodOptionsValidatorTests.cs`.
- [x] 1.3 Create `MarineOptionsValidator` in `src/Njord.OpenMeteo/Configuration/MarineOptionsValidator.cs`: extract `IValidateOptions<MarineOptions>`. Tests in `src/Njord.OpenMeteo.Tests/Configuration/MarineOptionsValidatorTests.cs`.
- [x] 1.4 Create `SeasonalOptionsValidator` in `src/Njord.OpenMeteo/Configuration/SeasonalOptionsValidator.cs`: extract `IValidateOptions<SeasonalOptions>`. Tests in `src/Njord.OpenMeteo.Tests/Configuration/SeasonalOptionsValidatorTests.cs`.
- [x] 1.5 Create `SatelliteOptionsValidator` in `src/Njord.OpenMeteo/Configuration/SatelliteOptionsValidator.cs`: extract `IValidateOptions<SatelliteOptions>`. Tests in `src/Njord.OpenMeteo.Tests/Configuration/SatelliteOptionsValidatorTests.cs`.
- [x] 1.6 Create `SolarYieldOptionsValidator` in `src/Njord.OpenMeteo/Configuration/SolarYieldOptionsValidator.cs`: extract `IValidateOptions<SolarYieldOptions>`. Tests in `src/Njord.OpenMeteo.Tests/Configuration/SolarYieldOptionsValidatorTests.cs`.
- [x] 1.7 Delete `OpenMeteoOptionsValidator` (`src/Njord.OpenMeteo/Configuration/OpenMeteoOptionsValidator.cs`) and its tests (`src/Njord.OpenMeteo.Tests/OpenMeteoOptionsValidatorTests.cs`). Update all `AddNjordOptions<T, OpenMeteoOptionsValidator>()` registrations in `OpenMeteoSetupContainer` to use the new per-type validators.

### Validation

```
dotnet run --project Njord.OpenMeteo.Tests/Njord.OpenMeteo.Tests.csproj
```

## 2. Resolve SolarYield PostConfigure Bridge

- [x] 2.1 Update `SolarYield` feature (`src/Njord.Features/Conditions/SolarYield/SolarYield.cs`): inject `IOptions<SolarYieldOptions>` alongside `IOptions<SolarYieldConfig>`. Read `PeakKw` and `SystemEfficiency` from `SolarYieldOptions` instead of `SolarYieldConfig`. Tests in `src/Njord.Features.Tests/Conditions/SolarYield/SolarYieldTests.cs`.
- [x] 2.2 Remove `PeakKw` and `SystemEfficiency` properties from `SolarYieldConfig` (`src/Njord.Features/Conditions/SolarYield/SolarYieldConfig.cs`).
- [x] 2.3 Remove the PostConfigure bridge from `DomainSetupContainer` (`src/Njord/Configuration/DomainSetupContainer.cs`).

### Validation

```
dotnet run --project Njord.Features.Tests/Njord.Features.Tests.csproj
```

## 3. Move FloodOptions to OpenMeteo

- [x] 3.1 ~~Move FloodOptions~~ SKIPPED: FloodOptions stays in Core because Polling depends on it and the architecture rule forbids Polling -> OpenMeteo. Update the namespace from `Njord.Core.Configuration` to `Njord.OpenMeteo.Configuration`. Add `using Njord.OpenMeteo.Configuration;` where needed (consumers: `BudgetGuard`, `PollingOptionsValidator`, `FloodClient`, tests). Register in `OpenMeteoSetupContainer` instead of `CoreSetupContainer` (if it was there).

### Validation

```
dotnet build Njord.slnx
dotnet run --project Njord.Polling.Tests/Njord.Polling.Tests.csproj
dotnet run --project Njord.OpenMeteo.Tests/Njord.OpenMeteo.Tests.csproj
```

## 4. Remove DefaultEnabled from FeatureDescriptor

- [x] 4.1 Remove `DefaultEnabled` parameter from `FeatureDescriptor` in `src/Njord.Core/Domain/FeatureDescriptor.cs`. Update all 66 feature classes that pass `DefaultEnabled:` in their descriptor construction (remove the named argument). Update the 5 test assertions that read `.Descriptor.DefaultEnabled`.

### Validation

```
dotnet build Njord.slnx
dotnet run --project Njord.Core.Tests/Njord.Core.Tests.csproj
dotnet run --project Njord.Features.Tests/Njord.Features.Tests.csproj
```

## 5. Full Test Suite + Formatting

- [x] 5.1 Run `dotnet format Njord.slnx` and verify.
- [x] 5.2 Run all test projects sequentially to confirm no regressions.

### Validation

```
dotnet format Njord.slnx --verify-no-changes
```

```bash
for p in Njord.*Tests; do
  [ "$p" = Njord.Tests.Shared ] && continue
  dotnet run --project "$p/$p.csproj" --no-build
done
```
