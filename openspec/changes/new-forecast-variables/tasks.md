## 1. Add thunderstorm_probability

- [x] 1.1 Add `ThunderstormProbability` to `ForecastParams` flags enum in `src/Njord.Core/Forecast/ForecastParams.cs`
- [x] 1.2 Add `ThunderstormProbability` to `ForecastPoint` in `src/Njord.Core/Forecast/ForecastPoint.cs`
- [x] 1.3 Add `ThunderstormProbability` member to `ForecastApiVariable` enum with `[JsonStringEnumMemberName("thunderstorm_probability")]`
- [x] 1.4 Add param-to-API-variable mapping
- [x] 1.5 Add response field to Forecast response DTO and map to `ForecastPoint`
- [x] 1.6 Add tests: param mapping, API variable name, response parsing

## 2. Add precipitation_type

- [x] 2.1 Add `PrecipitationType` to `ForecastParams` flags enum
- [x] 2.2 Add `PrecipitationType` to `ForecastPoint`
- [x] 2.3 Add `PrecipitationType` member to `ForecastApiVariable` enum with `[JsonStringEnumMemberName("precipitation_type")]`
- [x] 2.4 Add param-to-API-variable mapping
- [x] 2.5 Add response field and mapping
- [x] 2.6 Add tests

## 3. Add surface_temperature

- [x] 3.1 Add `SurfaceTemperature` to `ForecastParams` flags enum
- [x] 3.2 Add `SurfaceTemperature` to `ForecastPoint`
- [x] 3.3 Add `SurfaceTemperature` member to `ForecastApiVariable` enum with `[JsonStringEnumMemberName("surface_temperature")]`
- [x] 3.4 Add param-to-API-variable mapping
- [x] 3.5 Add response field and mapping
- [x] 3.6 Add tests

## 4. Add snowfall_water_equivalent

- [x] 4.1 Add `SnowfallWaterEquivalent` to `ForecastParams` flags enum
- [x] 4.2 Add `SnowfallWaterEquivalent` to `ForecastPoint`
- [x] 4.3 Add `SnowfallWaterEquivalent` member to `ForecastApiVariable` enum with `[JsonStringEnumMemberName("snowfall_water_equivalent")]`
- [x] 4.4 Add param-to-API-variable mapping
- [x] 4.5 Add response field and mapping
- [x] 4.6 Add tests

## 5. Validation

- [x] 5.1 Run `dotnet build src/Njord.slnx`
- [x] 5.2 Run `dotnet format src/Njord.slnx --verify-no-changes`
- [x] 5.3 Run all test projects sequentially
