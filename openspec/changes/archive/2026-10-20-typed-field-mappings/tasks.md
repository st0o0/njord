## 1. Request builder

- [x] 1.1 Specs for reserved characters, literal commas, idempotent `Build()` in `OpenMeteoUriBuilderSpec`
- [x] 1.2 Encode values with `Uri.EscapeDataString`, make `Build()` idempotent

## 2. Mappers

- [x] 2.1 Add `ForecastBuilding.BuildForecast<TPoint>`
- [x] 2.2 Weather, AirQuality, Marine use it with property access

## 3. Validation

- [x] 3.1 Build, `dotnet format --verify-no-changes`, all test projects green
