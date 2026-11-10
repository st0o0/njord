## 1. Extend WeatherResult with current-weather fields

- [x] 1.1 Add `double? CloudCover`, `double? DewPoint`, `double? Visibility`, `double? Precipitation`, `double? WindGusts` to `WeatherResult` in `src/Njord.Messages/Results/WeatherResult.cs` and add `string? ModelId = null`
- [x] 1.2 Add `ForecastParams.DewPoint`, `ForecastParams.Visibility`, `ForecastParams.WindGusts` flags to `src/Njord.Core/Forecast/ForecastParams.cs`; add corresponding `ForecastPoint` properties if missing in `src/Njord.Core/Forecast/ForecastPoint.cs`
- [x] 1.3 Update `WeatherRule.Descriptor.Requirements` in `src/Njord.Features/Weather/WeatherRule.cs` to include the new params; populate the new `WeatherResult` fields from `ForecastPoint` values at current time
- [x] 1.4 Update `WeatherRuleTests` in `src/Njord.Features.Tests/Weather/` to verify the new fields are populated and null when data is missing
- [x] 1.5 Map new `ForecastParams` flags to API variable names in `src/Njord.OpenMeteo/Mapping/ParamMappings.cs` (dew_point_2m, visibility, wind_gusts_10m); update `src/Njord.OpenMeteo.Tests/` mapping tests

## 2. Extend proto and gRPC mapping

- [x] 2.1 Add `optional double cloud_cover`, `optional double dew_point`, `optional double visibility`, `optional double precipitation`, `optional double wind_gusts` fields to `WeatherValue` in `protos/njord/njord.proto`
- [x] 2.2 Update `WeatherValueMappingExtensions` in `src/Njord.Grpc/Mapping/` to map the new `WeatherResult` fields to proto; update mapping tests in `src/Njord.Grpc.Tests/`

## 3. EntityRegistry per-model routing

- [x] 3.1 Add per-model entity IDs to `_ids` dictionary in `EntityRegistry` constructor (`src/Njord.Grpc/EntityRegistry.cs` lines 48-58) using compound key `(typeof(WeatherResult), $"{locationName}:{modelId}")`
- [x] 3.2 Extend `EntityRegistry.Find` to check `WeatherResult.ModelId` and use the compound key for per-model lookups
- [x] 3.3 Add tests in `src/Njord.Grpc.Tests/EntityRegistryTests.cs`: primary weather resolves, per-model weather resolves with ModelId, unknown ModelId returns null

## 4. FeatureEngine per-model WeatherRule execution

- [x] 4.1 In `FeatureEngine` constructor, find and store the single `OutputKind.Weather` rule as `_weatherRule` (`src/Njord.Pipeline/Stream/FeatureEngine.cs`)
- [x] 4.2 In the model-data branch (after line 48, before `return events`): if `_weatherRule` is non-null, build a temporary `LocationForecast` with the model's forecast as `Forecast`, create a 1-deep `RingBuffer`, call `_weatherRule.ExecuteAll`, and for each `WeatherResult` set `ModelId` to the received model ID; add results to `events`
- [x] 4.3 Add tests in `src/Njord.Pipeline.Tests/Stream/FeatureEngineTests.cs`: model data triggers WeatherRule, produces WeatherResult with correct ModelId; non-weather rules do NOT run on model data

## 5. ha-njord weather attribute mapping

- [x] 5.1 Add `native_wind_bearing` property returning `wind_direction` from the gRPC `WeatherValue` in `D:\GIT\ha-njord\custom_components\njord\weather.py`
- [x] 5.2 Add `cloud_coverage`, `native_dew_point`, `native_visibility` properties; map wind gusts if HA weather entity supports it; remove the hardcoded `cloud_coverage = None`
- [x] 5.3 Regenerate proto stubs in ha-njord for the new WeatherValue fields

## 6. Validation

- [x] 6.1 Run all test projects:
  ```
  dotnet run --project Njord.Core.Tests/Njord.Core.Tests.csproj
  dotnet run --project Njord.Messages.Tests/Njord.Messages.Tests.csproj
  dotnet run --project Njord.Features.Tests/Njord.Features.Tests.csproj
  dotnet run --project Njord.OpenMeteo.Tests/Njord.OpenMeteo.Tests.csproj
  dotnet run --project Njord.Pipeline.Tests/Njord.Pipeline.Tests.csproj
  dotnet run --project Njord.Grpc.Tests/Njord.Grpc.Tests.csproj
  dotnet run --project Njord.Architecture.Tests/Njord.Architecture.Tests.csproj
  ```
- [x] 6.2 Run `dotnet format Njord.slnx --verify-no-changes`
- [x] 6.3 E2E: verify per-model weather entities show data after Ensemble poll; verify primary weather entity shows cloud_cover, dew_point, visibility, wind_bearing (requires stack rebuild with new images)
