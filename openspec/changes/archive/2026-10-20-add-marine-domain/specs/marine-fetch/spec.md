## ADDED Requirements

### Requirement: Marine client fetches hourly wave and ocean data
The MarineClient SHALL fetch data from marine-api.open-meteo.com with hourly wave height, wave period, wave direction, ocean current velocity, ocean current direction, and sea surface temperature.

#### Scenario: Successful marine fetch
- **WHEN** FetchAsync is called with coastal coordinates
- **THEN** the response SHALL be parsed into a DomainForecast<MarinePoint> with hourly resolution

#### Scenario: Inland location returns empty
- **WHEN** FetchAsync is called with coordinates far from any ocean
- **THEN** the response SHALL return null or an empty forecast
