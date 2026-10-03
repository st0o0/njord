## ADDED Requirements

### Requirement: gRPC endpoint disables MinResponseDataRate
The gRPC HTTP/2 endpoint SHALL have Kestrel's `MinResponseDataRate` set to `null` so that long-lived server-streaming RPCs (e.g. `StreamConfig`) are not closed due to inactivity. gRPC uses its own HTTP/2 PING keepalive mechanism; Kestrel's response data rate enforcement is counterproductive for idle gRPC streams.

#### Scenario: StreamConfig stream stays open during extended inactivity
- **WHEN** a `StreamConfig` server stream is open and no config changes occur for 30 minutes
- **THEN** the stream SHALL remain connected and not be closed by Kestrel

#### Scenario: HTTP/1.1 endpoint retains default rate limits
- **WHEN** the HTTP/1.1 health endpoint serves a response
- **THEN** the default `MinResponseDataRate` behavior SHALL still apply (only the gRPC port is affected)
