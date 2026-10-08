## MODIFIED Requirements

### Requirement: Kestrel binds HTTP/1.1 and HTTP/2 on separate ports
Kestrel SHALL be configured with dual-port binding via declarative `Kestrel:Endpoints` configuration in `appsettings.json`: one endpoint for HTTP/1.1 (REST health endpoints, default port 8080) and one endpoint for HTTP/2 (gRPC h2c, default port 8081). Both endpoints SHALL operate without TLS. No code-based `ConfigureKestrel` endpoint setup SHALL be needed for port binding.

#### Scenario: REST endpoints served on HTTP/1.1 port
- **WHEN** a client sends an HTTP/1.1 GET to port 8080 `/alive`
- **THEN** the server SHALL respond with `200 OK`

#### Scenario: gRPC served on HTTP/2 port
- **WHEN** a gRPC client connects to port 8081 via `insecure_channel`
- **THEN** the server SHALL accept the h2c connection and serve gRPC requests

#### Scenario: gRPC on HTTP/1.1 port is rejected
- **WHEN** a gRPC client connects to port 8080
- **THEN** the connection SHALL fail (HTTP/1.1 does not support gRPC)

### Requirement: Ports are configurable via Kestrel endpoint config
The HTTP and gRPC ports SHALL be configurable via Kestrel's `Kestrel:Endpoints` configuration section. The HTTP endpoint SHALL default to `http://+:8080` and the gRPC endpoint SHALL default to `http://+:8081`. Ports SHALL be overridable via environment variables (e.g. `Kestrel__Endpoints__Grpc__Url=http://+:9081`).

#### Scenario: Custom gRPC port via environment variable
- **WHEN** `Kestrel__Endpoints__Grpc__Url` is set to `http://+:9090`
- **THEN** gRPC SHALL be served on port 9090 instead of 8081

#### Scenario: Custom HTTP port via environment variable
- **WHEN** `Kestrel__Endpoints__Http__Url` is set to `http://+:9080`
- **THEN** REST health endpoints SHALL be served on port 9080 instead of 8080

### Requirement: gRPC endpoint disables MinResponseDataRate
Kestrel SHALL have `MinResponseDataRate` set to `null` globally so that long-lived server-streaming RPCs (e.g. `StreamConfig`) are not closed due to inactivity. This is set in code via `ConfigureKestrel` because Kestrel's JSON configuration does not support `MinResponseDataRate`.

#### Scenario: StreamConfig stream stays open during extended inactivity
- **WHEN** a `StreamConfig` server stream is open and no config changes occur for 30 minutes
- **THEN** the stream SHALL remain connected and not be closed by Kestrel

### Requirement: Dockerfile exposes both ports
The Dockerfile SHALL expose both the HTTP port (8080) and the gRPC port (8081).

#### Scenario: Docker container accessible on both ports
- **WHEN** the njord container runs with `-p 8080:8080 -p 8081:8081`
- **THEN** both REST and gRPC endpoints SHALL be reachable from the host

## REMOVED Requirements

### Requirement: GrpcOptions class
**Reason**: Port configuration moves to Kestrel's `Kestrel:Endpoints:Grpc:Url` section. The separate `GrpcOptions` class with `Port` property is no longer needed.
**Migration**: Use `Kestrel__Endpoints__Grpc__Url` environment variable instead of `Njord__Grpc__Port`.

### Requirement: HTTP/1.1 endpoint retains default rate limits
**Reason**: `MinResponseDataRate` is now set to `null` globally rather than per-endpoint. The HTTP/1.1 port only serves short health check requests where `MinResponseDataRate` is irrelevant, so the per-endpoint distinction adds complexity without benefit.
**Migration**: None required. Health endpoint behavior is unchanged.
