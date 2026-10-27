## MODIFIED Requirements

### Requirement: Stream replay of cached state

When a gRPC client opens a server-streaming RPC (`StreamDomainData` or `StreamEnrichments`), the stream SHALL deliver all cached pipeline events before transitioning to live events from the BroadcastHub. The cached events are the same data that the unary RPCs (`GetDomainData`, `GetEnrichments`) return at that moment. After replay and live events, the stream SHALL include `KeepAlive` and `IdleTimeout` stages for client liveness detection, and a `Buffer` with `OverflowStrategy.Backpressure`.

#### Scenario: New subscriber receives cached domain data immediately

- **WHEN** the GrpcConsumer has received domain data for at least one location and a client requests a gRPC stream
- **THEN** the stream SHALL deliver the cached domain data events before any live events

#### Scenario: New subscriber receives cached feature results immediately

- **WHEN** the GrpcConsumer has received feature results for at least one location and a client requests a gRPC stream
- **THEN** the stream SHALL deliver the cached feature result events before any live events

#### Scenario: Empty cache produces no replay

- **WHEN** the GrpcConsumer has not received any pipeline events yet and a client requests a gRPC stream
- **THEN** the stream SHALL deliver no replay events and behave identically to a live-only stream

#### Scenario: Live events follow replay without gap

- **WHEN** a client connects and receives replayed cached events
- **AND** a new pipeline event arrives after the replay
- **THEN** the stream SHALL deliver the new event after the replayed events without requiring a reconnect
