## ADDED Requirements

### Requirement: Gateway routes fetches to the correct Open-Meteo endpoint
The Gateway SHALL route FetchDomain requests to the correct Open-Meteo API endpoint based on DomainKind: Weather→/v1/forecast or /v1/ensemble, AirQuality→/v1/air-quality, Flood→/v1/flood, Solar→/v1/satellite (future), Marine→/v1/marine (future).

#### Scenario: AirQuality routed to correct endpoint
- **WHEN** a FetchDomain(location, AirQuality) is received
- **THEN** the Gateway SHALL call the /v1/air-quality endpoint with the location's coordinates
