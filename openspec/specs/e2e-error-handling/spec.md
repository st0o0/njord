# e2e-error-handling Specification

## Purpose

Defines the E2E checks for gRPC error responses, Home Assistant REST API errors and service resilience under degraded conditions.

## Requirements

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
