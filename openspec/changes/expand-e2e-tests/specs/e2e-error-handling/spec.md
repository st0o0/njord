## ADDED Requirements

### Requirement: gRPC error responses for invalid requests

The E2E test plan SHALL verify that njord returns appropriate gRPC error codes for invalid requests.

#### Scenario: GetForecast with unknown location returns NOT_FOUND
- **WHEN** `WeatherService/GetForecast` is called with location "nonexistent_city"
- **THEN** the gRPC response is a NOT_FOUND or INVALID_ARGUMENT error status (not a crash or empty success)

#### Scenario: GetForecast with invalid model returns error
- **WHEN** `WeatherService/GetForecast` is called with model "fake_model_xyz"
- **THEN** the gRPC response is a NOT_FOUND or INVALID_ARGUMENT error status

#### Scenario: Push with invalid sensor kind returns error
- **WHEN** `SensorService/Push` is called with an unrecognized sensor kind
- **THEN** the gRPC response is an INVALID_ARGUMENT error status

#### Scenario: SetLocations with empty list returns error
- **WHEN** `AdminService/SetLocations` is called with an empty location list
- **THEN** the gRPC response is an INVALID_ARGUMENT error status (the system requires at least one location)

### Requirement: HA REST API error handling

The E2E test plan SHALL verify that HA REST API returns correct errors for njord-specific edge cases.

#### Scenario: Non-existent entity returns 404
- **WHEN** `GET /api/states/sensor.njord_nonexistent_sensor` is called
- **THEN** HA returns HTTP 404 or an empty/error response

#### Scenario: Invalid service call returns error
- **WHEN** `POST /api/services/weather/get_forecasts` is called with a non-weather entity_id
- **THEN** HA returns an error response (not a server crash)

### Requirement: Service resilience under degraded conditions

The E2E test plan SHALL verify that njord handles transient failures gracefully.

#### Scenario: njord recovers after container restart
- **WHEN** the njord container is restarted via `docker restart`
- **THEN** within 60 seconds njord responds to `/alive`, and within 120 seconds all gRPC streams reconnect and HA entities return to non-unavailable states

#### Scenario: Entity states during njord downtime
- **WHEN** the njord container is stopped for 30 seconds
- **THEN** HA weather entities show "unavailable" state (due to `expire_after` or availability topic), and recover after njord restarts
