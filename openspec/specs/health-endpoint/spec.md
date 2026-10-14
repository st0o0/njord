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

### Requirement: No additional endpoints are served
The service SHALL serve only `/healthz`, `/alive`, `/metrics` and the gRPC services. All other paths SHALL return `404`.

#### Scenario: Unknown path returns 404
- **WHEN** a `GET /other` request is received
- **THEN** the response status is `404`

### Requirement: The HTTP endpoint listens on a configurable port
The HTTP/1.1 endpoint serving the health, liveness and metrics routes SHALL listen on port 8080 by default, configured as `Kestrel:Endpoints:Http:Url` in `appsettings.json`, and SHALL be overridable through that key (for example `Kestrel__Endpoints__Http__Url`). Because explicit Kestrel endpoints are configured, `ASPNETCORE_URLS` SHALL NOT be used to change the port.

#### Scenario: Default port
- **WHEN** no Kestrel endpoint override is configured
- **THEN** Kestrel listens on `http://+:8080` for HTTP/1.1

#### Scenario: Custom port via Kestrel endpoint configuration
- **WHEN** `Kestrel__Endpoints__Http__Url` is set to `http://+:9090`
- **THEN** Kestrel serves the HTTP routes on port 9090

