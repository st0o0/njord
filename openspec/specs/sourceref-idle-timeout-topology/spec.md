# sourceref-idle-timeout-topology Specification

## Purpose
Stream topology tests proving that producer-side IdleTimeout fires under demand stall and consumer-side IdleTimeout survives upstream silence. These requirements document the fundamental Akka Streams stage-ordering behavior that governs SourceRef idle timeout correctness.

## Requirements

### Requirement: Producer-side IdleTimeout fires under demand stall
A stream topology with KeepAlive before Buffer before IdleTimeout before a SourceRef sink SHALL fail with a TimeoutException when the SourceRef consumer throttles demand, even though KeepAlive injects heartbeats into the Buffer. This proves the IdleTimeout stage counts only elements that pass through (pushed downstream), not elements buffered upstream.

#### Scenario: Throttled consumer triggers producer-side IdleTimeout
- **WHEN** a SourceRef producer pipeline uses KeepAlive(200ms) followed by Buffer followed by IdleTimeout(1s), and the consumer throttles to one element per 2 seconds
- **THEN** the producer stream SHALL fail with a TimeoutException within approximately 2 seconds

### Requirement: Consumer-side IdleTimeout survives upstream silence
A stream topology with KeepAlive before SourceRef on the producer, and IdleTimeout after SourceRef.Source on the consumer, SHALL remain alive indefinitely when the upstream is silent, because heartbeats cross the SourceRef and reset the consumer-side IdleTimeout.

#### Scenario: Silent upstream with consumer-side IdleTimeout stays alive
- **WHEN** a SourceRef producer pipeline uses KeepAlive(200ms) with no upstream data, and the consumer pipeline uses IdleTimeout(1s) followed by a heartbeat filter followed by Buffer
- **THEN** the stream SHALL remain alive for at least 3 seconds (3x the IdleTimeout) without a TimeoutException

### Requirement: Consumer-side IdleTimeout detects a dead producer
When no KeepAlive is configured and the producer sends no elements, the consumer-side IdleTimeout SHALL fire and tear down the stream.

#### Scenario: Producer sends nothing without KeepAlive
- **WHEN** a SourceRef producer pipeline has no KeepAlive and sends no elements, and the consumer pipeline uses IdleTimeout(1s)
- **THEN** the stream SHALL fail with a TimeoutException within approximately 2 seconds

### Requirement: Consumer-side Buffer decouples IdleTimeout from downstream backpressure
A Buffer between the heartbeat filter and the downstream consumer SHALL keep pulling demand through IdleTimeout even when the downstream consumer is slow, preventing false IdleTimeout triggers.

#### Scenario: Slow consumer with consumer-side Buffer does not trigger IdleTimeout
- **WHEN** a SourceRef producer pipeline uses KeepAlive(200ms), and the consumer pipeline uses IdleTimeout(1s) followed by a heartbeat filter followed by Buffer followed by Throttle(1 per 2s)
- **THEN** the stream SHALL remain alive for at least 3 seconds without a TimeoutException
