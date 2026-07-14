# health-endpoint Specification

## Purpose

HTTP health endpoint served by Kestrel for external monitoring. Returns the
aggregate result of registered health checks on `/healthz` and a simple
liveness probe on `/alive`.

## Requirements

### Requirement: Health endpoint responds on /healthz
The service SHALL expose an HTTP `GET /healthz` endpoint via Kestrel that
returns the aggregate result of all registered health checks. The response
status SHALL be `200 OK` when all checks are `Healthy`, `200 OK` with
`Degraded` body when any check is `Degraded`, and `503 Service Unavailable`
when any check is `Unhealthy`.

#### Scenario: All checks healthy
- **WHEN** MqttConnectionHealthCheck and PipelineHealthCheck both return
  `Healthy`
- **THEN** the `/healthz` response status is `200` and the body indicates
  `Healthy`

#### Scenario: One check degraded
- **WHEN** MqttConnectionHealthCheck returns `Degraded` and
  PipelineHealthCheck returns `Healthy`
- **THEN** the `/healthz` response status is `200` and the body indicates
  `Degraded`

#### Scenario: One check unhealthy
- **WHEN** PipelineHealthCheck returns `Unhealthy`
- **THEN** the `/healthz` response status is `503`

### Requirement: Liveness endpoint responds on /alive
The service SHALL expose an HTTP `GET /alive` endpoint that always returns
`200 OK` while the process is running. This endpoint SHALL NOT evaluate any
health checks.

#### Scenario: Alive response while running
- **WHEN** the service is running and a `GET /alive` request is received
- **THEN** the response status is `200` regardless of health check state

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
