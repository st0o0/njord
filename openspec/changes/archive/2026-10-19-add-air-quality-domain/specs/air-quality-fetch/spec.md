## ADDED Requirements

### Requirement: Air Quality endpoint fetches hourly AQ data
The OpenMeteoClient SHALL support fetching air quality data from `air-quality-api.open-meteo.com` with hourly resolution, returning PM2.5, PM10, ozone, pollen, and European AQI data as a `DomainForecast<AirQualityPoint>`.

#### Scenario: Successful AQ fetch
- **WHEN** FetchAirQualityAsync is called with valid coordinates
- **THEN** the response SHALL be parsed into a `DomainForecast<AirQualityPoint>` with hourly resolution

### Requirement: Gateway routes AirQuality domain
The OpenMeteoGateway SHALL route `FetchDomain` requests with `DomainKind.AirQuality` to the Air Quality endpoint and offer the result to the pipeline.

#### Scenario: AQ domain routed to correct endpoint
- **WHEN** the gateway receives `FetchDomain` with `DomainKind.AirQuality`
- **THEN** it SHALL call the air-quality-api endpoint and offer the resulting `DomainForecast<AirQualityPoint>` to the pipeline
