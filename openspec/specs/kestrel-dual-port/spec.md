# kestrel-dual-port Specification

## Purpose

Dual-port Kestrel binding separating HTTP/1.1 (REST health endpoints) from
HTTP/2 (gRPC h2c). Required because gRPC mandates HTTP/2 while health probes
and Docker HEALTHCHECK use HTTP/1.1. Both ports operate without TLS.

## Requirements

### Requirement: Kestrel binds HTTP/1.1 and HTTP/2 on separate ports
Kestrel SHALL be configured with explicit dual-port binding: one port for HTTP/1.1 (REST health endpoints) and one port for HTTP/2 (gRPC h2c). Both ports SHALL operate without TLS.

#### Scenario: REST endpoints served on HTTP/1.1 port
- **WHEN** a client sends an HTTP/1.1 GET to port 8080 `/alive`
- **THEN** the server SHALL respond with `200 OK`

#### Scenario: gRPC served on HTTP/2 port
- **WHEN** a gRPC client connects to port 8081 via `insecure_channel`
- **THEN** the server SHALL accept the h2c connection and serve gRPC requests

#### Scenario: gRPC on HTTP/1.1 port is rejected
- **WHEN** a gRPC client connects to port 8080
- **THEN** the connection SHALL fail (HTTP/1.1 does not support gRPC)

### Requirement: Ports are configurable
The HTTP and gRPC ports SHALL be configurable via `NjordOptions`. The HTTP port SHALL default to 8080 and the gRPC port SHALL default to 8081.

#### Scenario: Custom gRPC port via configuration
- **WHEN** `Njord:Grpc:Port` is set to `9090`
- **THEN** gRPC SHALL be served on port 9090 instead of 8081

### Requirement: Dockerfile exposes both ports
The Dockerfile SHALL expose both the HTTP port (8080) and the gRPC port (8081).

#### Scenario: Docker container accessible on both ports
- **WHEN** the njord container runs with `-p 8080:8080 -p 8081:8081`
- **THEN** both REST and gRPC endpoints SHALL be reachable from the host
