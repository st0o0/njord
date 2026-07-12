# health-endpoint Specification

## Purpose

HTTP health endpoint served by Kestrel for external monitoring. Returns a
fixed healthy response while the process runs; designed to be extended with
real checks (MQTT connection, poll freshness) in a future change.

## ADDED Requirements

### Requirement: Health endpoint responds on /healthz
The service SHALL expose an HTTP `GET /healthz` endpoint via Kestrel that
returns `200 OK` with body `Healthy` whenever the process is running.

#### Scenario: Healthy response while running
- **WHEN** the service is running and a `GET /healthz` request is received
- **THEN** the response status is `200` and the body is `Healthy`

### Requirement: Kestrel listens on a configurable port
The service SHALL listen on port 8080 by default (`ASPNETCORE_URLS`). The
port SHALL be overridable via the `ASPNETCORE_URLS` environment variable.

#### Scenario: Default port
- **WHEN** no `ASPNETCORE_URLS` is configured
- **THEN** Kestrel listens on `http://+:8080`

#### Scenario: Custom port via environment
- **WHEN** `ASPNETCORE_URLS` is set to `http://+:9090`
- **THEN** Kestrel listens on port 9090

### Requirement: No additional endpoints are served
The service SHALL serve only the health-check endpoint. All other paths SHALL
return `404`.

#### Scenario: Unknown path returns 404
- **WHEN** a `GET /other` request is received
- **THEN** the response status is `404`
