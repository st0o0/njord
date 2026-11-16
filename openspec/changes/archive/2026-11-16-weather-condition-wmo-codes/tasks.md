## 1. Messages: Add WeatherCode + IsDay to result records

- [x] 1.1 Add `int WeatherCode` and `bool IsDay` fields to `WeatherForecastPoint` in `src/Njord.Messages/Results/WeatherResult.cs` (positional, after `Condition`; optional params with defaults for `WeatherCode = 0`, `IsDay = true`)
- [x] 1.2 Add `int WeatherCode` and `bool IsDay` fields to `WeatherResult` in `src/Njord.Messages/Results/WeatherResult.cs` (positional, after `Condition`; optional params with defaults)

## 2. WeatherRule: Request IsDay and pass WMO codes through

- [x] 2.1 Add `ForecastParams.IsDay` to `WeatherRule.Descriptor.Requirements` in `src/Njord.Features/Weather/WeatherRule.cs`
- [x] 2.2 Update `WeatherRule.Evaluate` to pass `(int)(current.WeatherCode ?? 0)` and `(current.IsDay ?? 1.0) >= 1.0` into `WeatherResult`
- [x] 2.3 Update `WeatherRule.ToForecastPoint` to pass `(int)(p.WeatherCode ?? 0)` and `(p.IsDay ?? 1.0) >= 1.0` into `WeatherForecastPoint`
- [x] 2.4 Add tests in `src/Njord.Features.Tests/Weather/WeatherRuleTests.cs`: verify WeatherCode and IsDay are carried through for current and forecast points; verify IsDay defaults to true when null

## 3. Proto: Add weather_code + is_day fields

- [x] 3.1 Add `optional int32 weather_code = 14` and `optional bool is_day = 15` to `WeatherValue` in `protos/njord/njord.proto`
- [x] 3.2 Add `optional int32 weather_code = 12` and `optional bool is_day = 13` to `WeatherForecastPoint` in `protos/njord/njord.proto`

## 4. gRPC mapping: Send new fields

- [x] 4.1 Update `WeatherValueMappingExtensions.ToWeatherValue()` in `src/Njord.Grpc/Mapping/WeatherValueMappingExtensions.cs` to map `WeatherCode` and `IsDay`
- [x] 4.2 Update the `WeatherForecastPoint` mapping in the same file to map `WeatherCode` and `IsDay`
- [x] 4.3 Add/update tests in `src/Njord.Grpc.Tests/Mapping/WeatherValueMappingTests.cs`: verify weather_code and is_day are present in the proto output

## 5. ha-njord: Use condition_mapper for icon mapping

- [x] 5.1 Update `NjordWeatherEntity.condition` property in `D:\GIT\ha-njord\custom_components\njord\weather.py` to use `condition_mapper.map_condition(wv.weather_code, wv.is_day)` instead of `_CONDITION_MAP.get(wv.condition, wv.condition)`
- [x] 5.2 Update `_async_forecast_hourly` in the same file to use `condition_mapper.map_condition(point.weather_code, point.is_day)` for each forecast point's condition
- [x] 5.3 Remove the `_CONDITION_MAP` dictionary from `weather.py`
- [x] 5.4 Add the `condition_mapper` import to `weather.py`

## 6. Spec updates

- [x] 6.1 Update `openspec/specs/weather-feature/spec.md` main spec to reflect IsDay requirement and WeatherCode/IsDay on result records
- [x] 6.2 Update `openspec/specs/v3-proto/spec.md` main spec to include weather_code and is_day fields on WeatherValue and WeatherForecastPoint

## 7. Validation

- [x] 7.1 Build: `dotnet build Njord.slnx` from `src/`
- [x] 7.2 Format check: `dotnet format Njord.slnx --verify-no-changes` from `src/`
- [x] 7.3 Run touched test suites:
  - `dotnet run --project Njord.Messages.Tests/Njord.Messages.Tests.csproj`
  - `dotnet run --project Njord.Features.Tests/Njord.Features.Tests.csproj`
  - `dotnet run --project Njord.Grpc.Tests/Njord.Grpc.Tests.csproj`
- [x] 7.4 Run all test suites (snapshot tests may catch breaks in other projects)
