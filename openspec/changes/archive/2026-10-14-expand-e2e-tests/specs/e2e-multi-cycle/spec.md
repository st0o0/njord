## ADDED Requirements

### Requirement: Multi-poll-cycle data freshness

The E2E test plan SHALL verify that data updates across multiple poll cycles.

#### Scenario: Second poll cycle updates timestamps
- **WHEN** a second poll cycle is triggered via `TriggerPoll` after the first cycle completes
- **THEN** the `last_updated` timestamps on weather entities are more recent than after the first cycle

#### Scenario: Enrichment data refreshes across cycles
- **WHEN** a second poll cycle completes
- **THEN** enrichment sensors (alerts, indices, derived, trends) have `last_updated` timestamps from the second cycle

#### Scenario: Server usage increments across cycles
- **WHEN** `sensor.daily_usage` is read before and after a triggered poll
- **THEN** the usage count increases by the expected number of API calls (one per configured model)

### Requirement: Budget tracking accuracy

The E2E test plan SHALL verify that budget tracking reflects actual API usage.

#### Scenario: Budget status via gRPC matches HA entity
- **WHEN** `OpsService/GetStatus` budget fields are compared to `sensor.daily_usage` and `sensor.monthly_usage`
- **THEN** the values are consistent (same or within one poll cycle's worth of calls)

#### Scenario: Budget increases after triggered poll
- **WHEN** the budget values are read before and after a `TriggerPoll`
- **THEN** both daily and monthly usage values increase

### Requirement: Forecast data evolution

The E2E test plan SHALL verify that forecast data changes over successive cycles when the API returns updated data.

#### Scenario: Forecast entries may change between cycles
- **WHEN** hourly forecast entries are retrieved via HA `weather/get_forecasts` after two poll cycles
- **THEN** the forecast data is present and correctly structured in both cycles (values may or may not differ depending on API updates)

#### Scenario: Consensus recalculation on new data
- **WHEN** a new poll cycle completes with updated model forecasts
- **THEN** the consensus entity's temperature, agreement, and spread attributes reflect the latest poll data (timestamps update)
