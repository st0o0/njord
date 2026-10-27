# grpc-client-liveness Specification

## Purpose
TBD - created by archiving change gateway-wiretap-and-grpc-keepalive. Update Purpose after archive.
## Requirements
### Requirement: Heartbeat sentinel keeps idle streams alive
The GrpcConsumer per-client stream SHALL include a `KeepAlive` stage that injects a `PipelineEvent.Heartbeat` sentinel when no pipeline event has arrived within the keepalive interval. The keepalive interval SHALL be configurable and default to a value that covers one full poll interval plus margin (e.g. 90 seconds for a 60-minute poll cycle is too short -- the interval must exceed the poll interval). The Heartbeat event SHALL be a singleton `PipelineEvent` subtype that carries no data and is filtered out by the gRPC service layer before reaching the wire.

#### Scenario: Heartbeat injected during quiet period
- **WHEN** no pipeline event arrives for longer than the keepalive interval
- **THEN** the KeepAlive stage emits a Heartbeat event on the per-client stream

#### Scenario: Heartbeat does not reach the gRPC wire
- **WHEN** a Heartbeat event reaches the gRPC mapping layer
- **THEN** it SHALL be filtered out and not serialized to the client

#### Scenario: Normal events suppress heartbeats
- **WHEN** pipeline events arrive within the keepalive interval
- **THEN** no Heartbeat events are injected

### Requirement: Idle timeout detects dead gRPC clients
The GrpcConsumer per-client stream SHALL include an `IdleTimeout` stage that tears down the stream when no element has passed through for the idle timeout duration. The idle timeout SHALL be longer than the keepalive interval so that heartbeats prevent timeout for alive clients. A dead client that stops consuming causes backpressure that eventually starves the stream of elements, triggering the timeout.

#### Scenario: Dead client stream tears down
- **WHEN** a gRPC client stops consuming and no elements pass through the stream for the idle timeout duration
- **THEN** the stream SHALL fail with a timeout exception and release its BroadcastHub slot

#### Scenario: Slow but alive client survives
- **WHEN** a gRPC client consumes slowly but heartbeats continue to flow through the stream
- **THEN** the idle timeout SHALL NOT fire because elements (heartbeats) continue to pass

#### Scenario: Idle timeout failure is handled gracefully
- **WHEN** a per-client stream fails due to idle timeout
- **THEN** the GrpcConsumer SHALL log a warning and not crash or affect other client streams

### Requirement: Per-client buffer uses backpressure overflow
The per-client stream Buffer SHALL use `OverflowStrategy.Backpressure` instead of `DropHead`. When the buffer is full, backpressure SHALL propagate to the BroadcastHub rather than dropping older events.

#### Scenario: Slow client receives all events in order
- **WHEN** a gRPC client consumes slower than the pipeline produces
- **THEN** events SHALL accumulate in the buffer and none SHALL be dropped

#### Scenario: Buffer full applies backpressure
- **WHEN** the per-client buffer reaches capacity
- **THEN** the BroadcastHub connection for this client SHALL be backpressured until the client catches up

