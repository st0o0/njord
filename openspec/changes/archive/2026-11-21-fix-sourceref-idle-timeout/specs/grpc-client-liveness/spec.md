## MODIFIED Requirements

### Requirement: Heartbeat sentinel keeps idle streams alive
The GrpcConsumer per-client stream SHALL include a `KeepAlive` stage that injects a `ConsumerKeepAlive` sentinel when no entity event has arrived within the keepalive interval (30 seconds). The KeepAlive stage SHALL be placed on the producer side, upstream of the SourceRef materialization. The sentinel SHALL cross the SourceRef transport and be filtered out on the consumer side (in the gRPC service layer) after it has reset the consumer-side IdleTimeout.

#### Scenario: Heartbeat injected during quiet period
- **WHEN** no entity event arrives for longer than the keepalive interval
- **THEN** the KeepAlive stage emits a ConsumerKeepAlive event that flows through the SourceRef

#### Scenario: Heartbeat does not reach the gRPC wire
- **WHEN** a ConsumerKeepAlive event arrives on the consumer side of the SourceRef
- **THEN** it SHALL pass through the IdleTimeout stage (resetting its timer) and then be filtered out before proto mapping

#### Scenario: Normal events suppress heartbeats
- **WHEN** entity events arrive within the keepalive interval
- **THEN** no ConsumerKeepAlive events are injected

### Requirement: Idle timeout detects dead gRPC clients
The gRPC service consumer pipeline SHALL include an `IdleTimeout` stage placed immediately after the SourceRef source, before the heartbeat filter. The idle timeout (90 seconds) SHALL tear down the stream when no element (including heartbeats) has arrived from the SourceRef for the timeout duration. A dead producer or broken SourceRef connection that stops delivering elements triggers the timeout.

#### Scenario: Dead producer stream tears down
- **WHEN** the SourceRef stops delivering elements (producer dead or SourceRef connection broken) for the idle timeout duration
- **THEN** the consumer stream SHALL fail with a TimeoutException

#### Scenario: Slow but alive client survives
- **WHEN** a gRPC client consumes slowly but heartbeats continue to arrive through the SourceRef
- **THEN** the idle timeout SHALL NOT fire because heartbeats pass through IdleTimeout before being filtered

#### Scenario: Idle timeout failure is handled gracefully
- **WHEN** a consumer stream fails due to idle timeout
- **THEN** the gRPC service SHALL log the error and not crash or affect other Subscribe calls

### Requirement: Consumer-side buffer decouples IdleTimeout from gRPC write backpressure
The consumer pipeline SHALL include a `Buffer(128, Backpressure)` between the heartbeat filter and the gRPC write stages. This buffer SHALL keep pulling demand through the IdleTimeout stage even when gRPC WriteAsync temporarily stalls due to HTTP/2 flow control.

#### Scenario: gRPC write stall does not trigger IdleTimeout
- **WHEN** the gRPC WriteAsync temporarily blocks due to client-side flow control
- **THEN** the Buffer SHALL continue pulling elements through IdleTimeout, preventing a false timeout

#### Scenario: Buffer full applies backpressure to SourceRef
- **WHEN** the consumer-side buffer reaches capacity
- **THEN** backpressure SHALL propagate through IdleTimeout to the SourceRef, slowing the producer

### Requirement: Producer pipeline has no Buffer or IdleTimeout
The GrpcConsumer producer pipeline SHALL contain only the KeepAlive stage before the SourceRef materialization. No Buffer or IdleTimeout stage SHALL be present on the producer side.

#### Scenario: Producer pipeline is KeepAlive then SourceRef
- **WHEN** a client requests an entity stream
- **THEN** the materialized stream topology SHALL be: Source.From(initial).Concat(hubSource).KeepAlive(interval).RunWith(SourceRef)
