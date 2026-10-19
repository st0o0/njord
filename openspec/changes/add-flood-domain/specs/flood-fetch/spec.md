## ADDED Requirements

### Requirement: Flood endpoint fetches daily river discharge data
The OpenMeteoClient SHALL support fetching flood data from `flood-api.open-meteo.com` with daily resolution, returning river discharge forecasts and percentile data as a `DomainForecast<FloodPoint>` with `TimeResolution.Daily`.

#### Scenario: Successful flood fetch
- **WHEN** FetchFloodAsync is called with valid coordinates
- **THEN** the response SHALL be parsed into a `DomainForecast<FloodPoint>` with daily resolution and discharge values

### Requirement: Gateway routes Flood domain
The OpenMeteoGateway SHALL route `FetchDomain` requests with `DomainKind.Flood` to the Flood API endpoint and offer the result to the pipeline.

#### Scenario: Flood domain routed to correct endpoint
- **WHEN** the gateway receives `FetchDomain` with `DomainKind.Flood`
- **THEN** it SHALL call the flood-api endpoint and offer the resulting `DomainForecast<FloodPoint>` to the pipeline
