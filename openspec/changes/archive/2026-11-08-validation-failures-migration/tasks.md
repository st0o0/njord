## 1. Prerequisite

- [x] 1.1 Bump `Terp` and `Terp.Akka` in `src/Directory.Packages.props` to the first release containing `ValidationFailures`. Verify with `dotnet build src/Njord.slnx`.

## 2. Pilot - HeatConfigValidator

- [x] 2.1 Convert `src/Njord.Features/Alerts/Heat/HeatConfigValidator.cs` from `List<string>` to `ValidationFailures`. Pass section to constructor, use `Require()` for `WindowHours`, `Add()` for `ThresholdValidation.Violation` with bare `"Thresholds"` path. Run `src/Njord.Features.Tests` to confirm messages are identical.

## 3. Alert validators (remaining 22)

- [x] 3.1 Convert the 12 alert validators that call `ThresholdValidation.Violation`: `Frost`, `Storm`, `Snow`, `Humidity`, `WindChill`, `Visibility`, `TropicalNight`, `ClearSkyUv`, `WetBulbDanger`, `Dust`, `RainProbability`, `Shower` (`src/Njord.Features/Alerts/<Name>/<Name>ConfigValidator.cs`). All switch to bare property name for `ThresholdValidation` path.
- [x] 3.2 Convert the 10 alert validators without `ThresholdValidation`: `Fog`, `Ice`, `SmogRisk`, `PollenBlast`, `MigraineRisk`, `HeavyRain`, `CoastalFlood`, `FloodRisk`, `FloodRain`, `PressureDrop` (`src/Njord.Features/Alerts/<Name>/<Name>ConfigValidator.cs`).
- [x] 3.3 Run `dotnet run --project src/Njord.Features.Tests/Njord.Features.Tests.csproj` - all tests pass.

## 4. Conditions validators (7)

- [x] 4.1 Convert `IrrigationNeed`, `Evapotranspiration`, `SnowDepthWatch`, `FrostDepth`, `OceanCurrents`, `HeatPumpCop`, `SoilMoisture` config validators (`src/Njord.Features/Conditions/<Name>/<Name>ConfigValidator.cs`).
- [x] 4.2 Run `dotnet run --project src/Njord.Features.Tests/Njord.Features.Tests.csproj` - all tests pass.

## 5. Scores validators (5)

- [x] 5.1 Convert `Laundry`, `AllergySeverity`, `SleepQuality`, `SprayWindow`, `AsthmaRisk` config validators (`src/Njord.Features/Scores/<Name>/<Name>ConfigValidator.cs`).
- [x] 5.2 Run `dotnet run --project src/Njord.Features.Tests/Njord.Features.Tests.csproj` - all tests pass.

## 6. Forecasts validators (1)

- [x] 6.1 Convert `GrowingDegree` config validator (`src/Njord.Features/Forecasts/GrowingDegree/GrowingDegreeConfigValidator.cs`).
- [x] 6.2 Run `dotnet run --project src/Njord.Features.Tests/Njord.Features.Tests.csproj` - all tests pass.

## 7. Non-feature validators (2)

- [x] 7.1 Convert `PollingOptionsValidator` (`src/Njord.Polling/Configuration/PollingOptionsValidator.cs`) - no section prefix, preserve budget-check guard (`failures.Count == 0 && ...`).
- [x] 7.2 Convert `HealthOptionsValidator` (`src/Njord.Core/Configuration/HealthOptionsValidator.cs`) - no section prefix.
- [x] 7.3 Run `dotnet run --project src/Njord.Polling.Tests/Njord.Polling.Tests.csproj` and `dotnet run --project src/Njord.Core.Tests/Njord.Core.Tests.csproj` - all tests pass.

## 8. Validation

- [x] 8.1 `dotnet build src/Njord.slnx` - clean build.
- [x] 8.2 `dotnet format src/Njord.slnx --verify-no-changes` - no formatting violations.
- [x] 8.3 Run all test projects sequentially - all pass.
