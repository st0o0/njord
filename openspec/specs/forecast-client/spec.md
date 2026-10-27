# forecast-client Specification

## Purpose

ForecastClient as the primary weather data source using the deterministic Open-Meteo Forecast API (api.open-meteo.com/v1/forecast) with automatic best_match model blending.

## Requirements

### Requirement: ForecastClient fetches from the deterministic Forecast API
The `ForecastClient` SHALL be an `IDomainClient` with `Domain = DomainKind.Weather`. It SHALL fetch from `api.open-meteo.com/v1/forecast` without a `models` parameter, using the API's automatic best_match model blending.

#### Scenario: No models parameter
- **WHEN** the ForecastClient builds the request URL
- **THEN** the URL SHALL NOT contain a `models` query parameter

#### Scenario: Deterministic response with plain keys
- **WHEN** the Forecast API returns data
- **THEN** the response SHALL contain plain hourly keys (e.g. `temperature_2m`) without model suffixes

### Requirement: ForecastClient is the primary weather data source
The `ForecastClient` SHALL be the sole source for `DomainKind.Weather` data in the pipeline. Its `FetchSuccess` SHALL carry a `DomainForecast<WeatherPoint>` as the primary forecast. `WeatherPoint` SHALL NOT contain spread fields.

#### Scenario: Weather entity uses forecast data
- **WHEN** Home Assistant displays `weather.lucerne`
- **THEN** the entity's temperature, humidity, wind speed, and condition SHALL come from the ForecastClient's deterministic forecast

#### Scenario: precipitation_probability available
- **WHEN** the ForecastClient fetches weather data
- **THEN** `WeatherPoint.PrecipitationProbability` SHALL contain the API's native value (not null)

#### Scenario: uv_index available
- **WHEN** the ForecastClient fetches weather data
- **THEN** `WeatherPoint.UvIndex` SHALL contain a numeric value (not null as with the Ensemble API)

### Requirement: ForecastClient call weight
The ForecastClient SHALL report a `CallWeight` of `ceil(variables / 10) * ceil(forecastDays / 14)`. There is no model multiplier because best_match is a single logical model.

#### Scenario: Standard variable set
- **WHEN** 31 weather API variables are resolved
- **THEN** the ForecastClient's `CallWeight` SHALL be 4

### Requirement: ForecastClient reuses existing response mapping
The ForecastClient SHALL reuse `WeatherResponse`, `WeatherHourlyData`, and the `SplitByModel` mapping. The Forecast API's plain keys (no model suffix) SHALL be handled by the existing empty-key path in `SplitByModel`, producing a single forecast keyed by a synthetic model id.

#### Scenario: Plain keys mapped correctly
- **WHEN** the Forecast API returns `temperature_2m` without a model suffix
- **THEN** the mapping SHALL produce one forecast entry, not zero
