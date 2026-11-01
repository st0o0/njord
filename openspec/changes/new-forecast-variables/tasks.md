## 1. Add thunderstorm_probability

- [ ] 1.1 Add `ThunderstormProbability` to `ForecastParams` flags enum in `src/Njord.Core/Forecast/ForecastParams.cs`
- [ ] 1.2 Add `ThunderstormProbability` to `ForecastPoint` in `src/Njord.Core/Forecast/ForecastPoint.cs`
- [ ] 1.3 Add `ThunderstormProbability` member to `ForecastApiVariable` enum with `[JsonStringEnumMemberName("thunderstorm_probability")]`
- [ ] 1.4 Add param-to-API-variable mapping
- [ ] 1.5 Add response field to Forecast response DTO and map to `ForecastPoint`
- [ ] 1.6 Add tests: param mapping, API variable name, response parsing

## 2. Add precipitation_type

- [ ] 2.1 Add `PrecipitationType` to `ForecastParams` flags enum
- [ ] 2.2 Add `PrecipitationType` to `ForecastPoint`
- [ ] 2.3 Add `PrecipitationType` member to `ForecastApiVariable` enum with `[JsonStringEnumMemberName("precipitation_type")]`
- [ ] 2.4 Add param-to-API-variable mapping
- [ ] 2.5 Add response field and mapping
- [ ] 2.6 Add tests

## 3. Add surface_temperature

- [ ] 3.1 Add `SurfaceTemperature` to `ForecastParams` flags enum
- [ ] 3.2 Add `SurfaceTemperature` to `ForecastPoint`
- [ ] 3.3 Add `SurfaceTemperature` member to `ForecastApiVariable` enum with `[JsonStringEnumMemberName("surface_temperature")]`
- [ ] 3.4 Add param-to-API-variable mapping
- [ ] 3.5 Add response field and mapping
- [ ] 3.6 Add tests

## 4. Add snowfall_water_equivalent

- [ ] 4.1 Add `SnowfallWaterEquivalent` to `ForecastParams` flags enum
- [ ] 4.2 Add `SnowfallWaterEquivalent` to `ForecastPoint`
- [ ] 4.3 Add `SnowfallWaterEquivalent` member to `ForecastApiVariable` enum with `[JsonStringEnumMemberName("snowfall_water_equivalent")]`
- [ ] 4.4 Add param-to-API-variable mapping
- [ ] 4.5 Add response field and mapping
- [ ] 4.6 Add tests

## 5. Validation

- [ ] 5.1 Run `dotnet build src/Njord.slnx`
- [ ] 5.2 Run `dotnet format src/Njord.slnx --verify-no-changes`
- [ ] 5.3 Run all test projects sequentially
