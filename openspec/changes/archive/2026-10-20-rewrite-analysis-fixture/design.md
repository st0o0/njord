## Context

AnalysisFixture builds multi-model forecasts and consensus — architecture that no longer exists. Every test that uses it is building data through an obsolete abstraction.

## Goals / Non-Goals

**Goals:** Clean test builders for DomainForecast<WeatherPoint>, AirQualityPoint, FloodPoint.
**Non-Goals:** Changing test assertions or coverage.

## Decisions

### D1: Fluent builder pattern
`WeatherForecastBuilder.Default().WithTemperature(hour: 6, value: -2.0).Build()` returns a `DomainForecast<WeatherPoint>`. Sensible defaults for all fields. Override only what the test cares about.

### D2: One builder per domain
`WeatherForecastBuilder`, `AirQualityForecastBuilder`, `FloodForecastBuilder` — each knows its domain's typical values and resolution.

### D3: LocationForecastBuilder for multi-domain tests
`LocationForecastBuilder.Default(location).WithWeather(forecast).WithFlood(forecast).Build()` for tests that need a full LocationForecast.
