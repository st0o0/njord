## MODIFIED Requirements

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

## ADDED Requirements

### Requirement: Liveness endpoint responds on /alive
The service SHALL expose an HTTP `GET /alive` endpoint that always returns
`200 OK` while the process is running. This endpoint SHALL NOT evaluate any
health checks.

#### Scenario: Alive response while running
- **WHEN** the service is running and a `GET /alive` request is received
- **THEN** the response status is `200` regardless of health check state
