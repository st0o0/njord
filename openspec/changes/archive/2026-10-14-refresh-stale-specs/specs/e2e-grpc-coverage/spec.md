## MODIFIED Requirements

### Requirement: WeatherService RPC coverage

The E2E test plan SHALL exercise every RPC in `njord.v2.WeatherService`.

#### Scenario: GetCatalog returns location and model metadata
- **WHEN** `grpcurl -plaintext localhost:8081 njord.v2.WeatherService/GetCatalog` is called with no filter
- **THEN** the response contains at least one location with name "lucerne" and at least two models matching the configured set (icon_d2, ecmwf_ifs025)

#### Scenario: GetForecast returns hourly data for a specific location and model
- **WHEN** `GetForecast` is called with location "lucerne" and model "icon_d2"
- **THEN** the response contains hourly forecast entries with numeric temperature, humidity, and wind_speed values, and timestamps within the configured horizon range

#### Scenario: GetEnrichments returns enrichment results for a location
- **WHEN** `GetEnrichments` is called with location "lucerne"
- **THEN** the response contains enrichment results for each enabled feature (consensus, alerts, derived, trends, indices)

#### Scenario: StreamForecasts delivers forecast updates
- **WHEN** `StreamForecasts` is opened and a poll cycle completes (triggered via TriggerPoll)
- **THEN** the stream delivers at least one `ForecastUpdate` message within 120 seconds

#### Scenario: StreamEnrichments delivers enrichment events
- **WHEN** `StreamEnrichments` is opened and a poll cycle completes
- **THEN** the stream delivers at least one `EnrichmentEvent` message within 120 seconds
