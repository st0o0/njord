## ADDED Requirements

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
- **THEN** the response contains enrichment results for each enabled feature (consensus, alerts, derived, trends, indices, history)

#### Scenario: StreamForecasts delivers forecast updates
- **WHEN** `StreamForecasts` is opened and a poll cycle completes (triggered via TriggerPoll)
- **THEN** the stream delivers at least one `ForecastUpdate` message within 120 seconds

#### Scenario: StreamEnrichments delivers enrichment events
- **WHEN** `StreamEnrichments` is opened and a poll cycle completes
- **THEN** the stream delivers at least one `EnrichmentEvent` message within 120 seconds

### Requirement: OpsService RPC coverage

The E2E test plan SHALL exercise every RPC in `njord.v2.OpsService`.

#### Scenario: GetStatus returns system status
- **WHEN** `OpsService/GetStatus` is called
- **THEN** the response contains a `version` string, a locations array with "lucerne", and models matching config

#### Scenario: GetTargets returns model targets
- **WHEN** `OpsService/GetTargets` is called
- **THEN** the response contains target entries for each configured model with numeric next-poll timestamps

#### Scenario: TriggerPoll initiates an immediate poll cycle
- **WHEN** `OpsService/TriggerPoll` is called
- **THEN** the response indicates success, and within 60 seconds, weather entity `last_updated` timestamps in HA are refreshed

### Requirement: AdminService RPC coverage

The E2E test plan SHALL exercise every RPC in `njord.v2.AdminService`.

#### Scenario: GetConfig returns the running configuration
- **WHEN** `AdminService/GetConfig` is called
- **THEN** the response contains locations, models, enrichment settings, and budget settings matching the Docker Compose config

#### Scenario: StreamConfig delivers config change events
- **WHEN** `StreamConfig` is opened and a config mutation is applied (e.g., SetSettings)
- **THEN** the stream delivers the updated config within 30 seconds

#### Scenario: SetLocations updates the location list
- **WHEN** `SetLocations` is called adding a second location
- **THEN** the response indicates success and a subsequent `GetConfig` reflects the new location list

#### Scenario: SetSettings updates poll interval or other settings
- **WHEN** `SetSettings` is called with a modified poll interval
- **THEN** the response indicates success and a subsequent `GetConfig` reflects the changed setting

#### Scenario: SetEnrichment toggles an enrichment feature
- **WHEN** `SetEnrichment` is called disabling the "trends" feature
- **THEN** the response indicates success and a subsequent `GetConfig` shows trends disabled

#### Scenario: SetBudget updates the budget override
- **WHEN** `SetBudget` is called with a custom monthly limit
- **THEN** the response indicates success and a subsequent `GetConfig` reflects the new budget

### Requirement: SensorService RPC coverage

The E2E test plan SHALL exercise every RPC in `njord.v2.SensorService`.

#### Scenario: Push sends a single sensor reading
- **WHEN** `SensorService/Push` is called with a valid SensorReading (kind: INDOOR_TEMPERATURE, value: 21.5)
- **THEN** the response indicates success

#### Scenario: StreamPush sends a batch of sensor readings
- **WHEN** `SensorService/StreamPush` is opened and multiple SensorReading messages are sent
- **THEN** the response indicates success after the stream closes
