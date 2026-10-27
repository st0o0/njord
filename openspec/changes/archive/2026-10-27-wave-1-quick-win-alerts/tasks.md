## 1. Infrastructure: UvIndexClearSky param

- [x] 1.1 Add `UvIndexClearSky = 1L << 32` to `WeatherParams` in `src/Njord.Forecast/Weather/WeatherParams.cs`
- [x] 1.2 Add `double? UvIndexClearSky = null` field to `WeatherPoint` in `src/Njord.Forecast/Weather/WeatherPoint.cs`. Add `HasFields` case for the new flag
- [x] 1.3 Add `UvIndexClearSky` to `WeatherApiVariable` with `[JsonStringEnumMemberName("uv_index_clear_sky")]` in `src/Njord.OpenMeteo/Weather/WeatherApiVariable.cs`
- [x] 1.4 Add `UvIndexClearSky` mapping in `src/Njord.OpenMeteo/Weather/WeatherApiVariableMappingExtensions.cs` (both directions)
- [x] 1.5 Add `[property: JsonPropertyName("uv_index_clear_sky")] double?[]? UvIndexClearSky = null` to `WeatherHourlyData` in `src/Njord.OpenMeteo/Weather/WeatherResponse.cs`. Add `UvIndexClearSky: At(h.UvIndexClearSky, i)` to the `WeatherPoint` construction in `src/Njord.OpenMeteo/Weather/WeatherResponseMappingExtensions.cs`
- [x] 1.6 Tests: update `src/Njord.Forecast.Tests/Weather/WeatherPointTests.cs` for new field. Add theory case in `src/Njord.OpenMeteo.Tests/Mapping/ParamsMappingsTests.cs`

### Validation
```
dotnet run --project Njord.Forecast.Tests/Njord.Forecast.Tests.csproj
dotnet run --project Njord.OpenMeteo.Tests/Njord.OpenMeteo.Tests.csproj
```

## 2. RainProbability Alert

- [x] 2.1 Create `RainProbabilityConfig : IConfigSection` in `src/Njord.Features/Alerts/RainProbability/RainProbabilityConfig.cs` with `Enabled`, `WindowHours` (default 6), `ModerateThreshold` (default 50), `HighThreshold` (default 70), `ExtremeThreshold` (default 90)
- [x] 2.2 Create `RainProbabilityAlert : FeatureRule<WeatherEnsembleInput>` in `src/Njord.Features/Alerts/RainProbability/RainProbabilityAlert.cs`. Descriptor: `OutputKind.Alert`, `FeatureCategory.Weather`, `DefaultEnabled: true`, `Requirements: Weather(PrecipitationProbability), Ensemble(TemperatureSpread)`. Evaluate: scan next WindowHours, find peak probability, map to severity
- [x] 2.3 Register in `src/Njord.Features/ServiceCollectionExtensions.cs`: `AddNjordOptions<RainProbabilityConfig>()` and `AddSingleton<IFeatureRule, RainProbabilityAlert>()`
- [x] 2.4 Tests: create `src/Njord.Features.Tests/Alerts/RainProbability/RainProbabilityAlertTests.cs` with scenarios: high probability fires, moderate fires, below threshold skips, window config works, missing ensemble skips

### Validation
```
dotnet run --project Njord.Features.Tests/Njord.Features.Tests.csproj
```

## 3. WetBulbDanger Alert

- [x] 3.1 Create `WetBulbDangerConfig : IConfigSection` in `src/Njord.Features/Alerts/WetBulbDanger/WetBulbDangerConfig.cs` with `Enabled`, `YellowThreshold` (default 26), `OrangeThreshold` (default 28), `RedThreshold` (default 32)
- [x] 3.2 Create `WetBulbDangerAlert : FeatureRule<WeatherEnsembleInput>` in `src/Njord.Features/Alerts/WetBulbDanger/WetBulbDangerAlert.cs`. Descriptor: `OutputKind.Alert`, `FeatureCategory.Weather`, `DefaultEnabled: false`, `Requirements: Weather(WetBulbTemperature), Ensemble(TemperatureSpread)`. Evaluate: find peak wet-bulb temp, map to severity
- [x] 3.3 Register in `src/Njord.Features/ServiceCollectionExtensions.cs`
- [x] 3.4 Tests: create `src/Njord.Features.Tests/Alerts/WetBulbDanger/WetBulbDangerAlertTests.cs` with scenarios: orange fires at 28+, red fires at 32+, below 26 skips, ensemble confidence works

### Validation
```
dotnet run --project Njord.Features.Tests/Njord.Features.Tests.csproj
```

## 4. ClearSkyUv Alert

- [x] 4.1 Create `ClearSkyUvConfig : IConfigSection` in `src/Njord.Features/Alerts/ClearSkyUv/ClearSkyUvConfig.cs` with `Enabled`, `ModerateThreshold` (default 6), `HighThreshold` (default 8), `ExtremeThreshold` (default 10)
- [x] 4.2 Create `ClearSkyUvAlert : FeatureRule<WeatherInput>` in `src/Njord.Features/Alerts/ClearSkyUv/ClearSkyUvAlert.cs`. Descriptor: `OutputKind.Alert`, `FeatureCategory.Lifestyle`, `DefaultEnabled: false`, `Requirements: Weather(UvIndexClearSky | IsDay)`. No Ensemble -- confidence is always Medium. Evaluate: filter to daytime hours only (IsDay > 0), find peak clear-sky UV, map to severity
- [x] 4.3 Register in `src/Njord.Features/ServiceCollectionExtensions.cs`
- [x] 4.4 Tests: create `src/Njord.Features.Tests/Alerts/ClearSkyUv/ClearSkyUvAlertTests.cs` with scenarios: high UV fires, below threshold skips, nighttime hours excluded, Medium confidence always

### Validation
```
dotnet run --project Njord.Features.Tests/Njord.Features.Tests.csproj
```

## 5. SnowDepthWatch Condition

- [x] 5.1 Create `SnowDepthWatchConfig : IConfigSection` in `src/Njord.Features/Conditions/SnowDepthWatch/SnowDepthWatchConfig.cs` with `Enabled`, `LightThreshold` (default 2), `ModerateThreshold` (default 10), `HeavyThreshold` (default 25), `ExtremeThreshold` (default 50)
- [x] 5.2 Create `SnowDepthWatch : FeatureRule<WeatherInput>` in `src/Njord.Features/Conditions/SnowDepthWatch/SnowDepthWatch.cs`. Descriptor: `OutputKind.Condition`, `FeatureCategory.Weather`, `DefaultEnabled: false`, `Requirements: Weather(SnowDepth)`. No Ensemble -- Medium confidence. Evaluate: find max snow depth, map to level, skip when below LightThreshold
- [x] 5.3 Register in `src/Njord.Features/ServiceCollectionExtensions.cs`
- [x] 5.4 Tests: create `src/Njord.Features.Tests/Conditions/SnowDepthWatch/SnowDepthWatchTests.cs` with scenarios: moderate depth fires, below light skips, extreme depth, Medium confidence

### Validation
```
dotnet run --project Njord.Features.Tests/Njord.Features.Tests.csproj
```

## 6. Integration

- [x] 6.1 Add result records for the 4 new features in `src/Njord.Messages/` following existing patterns (`RainProbabilityAlertResult`, `WetBulbDangerAlertResult`, `ClearSkyUvAlertResult`, `SnowDepthWatchConditionResult`)
- [x] 6.2 Add proto messages and oneof cases for the 4 features in `protos/njord/v2/weather.proto`. Add gRPC mapping in `src/Njord.Grpc/Mapping/`
- [x] 6.3 Update `docs/data/features.json` with 4 new feature entries including domain requirements, thresholds, and descriptions
- [x] 6.4 Update Config Builder (`docs/.vitepress/theme/builder/`) for the 4 new features
- [x] 6.5 Update `e2e/docker-compose.e2e.yml` to enable RainProbability (default on) and optionally the others
- [x] 6.6 Update ha-njord: add entity mappings for the 4 new features in `D:\GIT\ha-njord\custom_components\njord\`

### Validation
```
dotnet run --project Njord.Grpc.Tests/Njord.Grpc.Tests.csproj
dotnet run --project Njord.Messages.Tests/Njord.Messages.Tests.csproj
```

## 7. Full Validation

- [x] 7.1 Run `dotnet build Njord.slnx` and `dotnet format Njord.slnx --verify-no-changes`
- [x] 7.2 Run all test suites sequentially:
```bash
for p in Njord.*Tests; do
  [ "$p" = Njord.Tests.Shared ] && continue
  dotnet run --project "$p/$p.csproj" --no-build
done
```
- [x] 7.3 Update `AGENTS.md` feature count and alert list if needed
