## ADDED Requirements

### Requirement: The HTTP endpoint listens on a configurable port
The HTTP/1.1 endpoint serving the health, liveness and metrics routes SHALL listen on port 8080 by default, configured as `Kestrel:Endpoints:Http:Url` in `appsettings.json`, and SHALL be overridable through that key (for example `Kestrel__Endpoints__Http__Url`). Because explicit Kestrel endpoints are configured, `ASPNETCORE_URLS` SHALL NOT be used to change the port.

#### Scenario: Default port
- **WHEN** no Kestrel endpoint override is configured
- **THEN** Kestrel listens on `http://+:8080` for HTTP/1.1

#### Scenario: Custom port via Kestrel endpoint configuration
- **WHEN** `Kestrel__Endpoints__Http__Url` is set to `http://+:9090`
- **THEN** Kestrel serves the HTTP routes on port 9090

## MODIFIED Requirements

### Requirement: No additional endpoints are served
The service SHALL serve only `/healthz`, `/alive`, `/metrics` and the gRPC services. All other paths SHALL return `404`.

#### Scenario: Unknown path returns 404
- **WHEN** a `GET /other` request is received
- **THEN** the response status is `404`

## REMOVED Requirements

### Requirement: Kestrel listens on a configurable port
**Reason**: The requirement named types, files or behavior that no longer exist; it is restated against the current code.
**Migration**: Superseded by "The HTTP endpoint listens on a configurable port" in this capability.
