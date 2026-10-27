## ADDED Requirements

### Requirement: Stream replay of cached state

When a gRPC client opens a server-streaming RPC (`StreamDomainData` or
`StreamEnrichments`), the stream SHALL deliver all cached pipeline events
before transitioning to live events from the BroadcastHub. The cached events
are the same data that the unary RPCs (`GetDomainData`, `GetEnrichments`)
return at that moment.

#### Scenario: New subscriber receives cached domain data immediately

- **WHEN** the GrpcConsumer has received domain data for at least one location
  and a client requests a gRPC stream
- **THEN** the stream SHALL deliver the cached domain data events before any
  live events

#### Scenario: New subscriber receives cached feature results immediately

- **WHEN** the GrpcConsumer has received feature results for at least one
  location and a client requests a gRPC stream
- **THEN** the stream SHALL deliver the cached feature result events before
  any live events

#### Scenario: Empty cache produces no replay

- **WHEN** the GrpcConsumer has not received any pipeline events yet and a
  client requests a gRPC stream
- **THEN** the stream SHALL deliver no replay events and behave identically
  to a live-only stream

#### Scenario: Live events follow replay without gap

- **WHEN** a client connects and receives replayed cached events
- **AND** a new pipeline event arrives after the replay
- **THEN** the stream SHALL deliver the new event after the replayed events
  without requiring a reconnect

### Requirement: Existing stream filtering unchanged

The downstream filtering in the gRPC service layer SHALL continue to work
identically for both replayed and live events. No new filtering logic is
introduced.

#### Scenario: StreamDomainData filters replayed enrichment events

- **WHEN** a StreamDomainData client receives replayed events that include
  both domain data and feature results
- **THEN** the existing domain-kind filter SHALL pass only domain data events
  to the client response stream

#### Scenario: StreamEnrichments filters replayed domain events

- **WHEN** a StreamEnrichments client receives replayed events that include
  both domain data and feature results
- **THEN** the existing enrichment filter SHALL pass only feature result
  events to the client response stream
