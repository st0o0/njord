## MODIFIED Requirements

### Requirement: Shared logging decider for stream supervision
The system SHALL provide a `StreamSupervision.LoggingDecider(ILogger)` static
factory that returns an Akka.Streams `Decider`. Before classifying, the decider
SHALL unwrap an `AggregateException` that has exactly one inner exception,
repeatedly, and classify the innermost single cause; an `AggregateException`
with several inner exceptions SHALL be classified as is. The decider SHALL log
every exception at Warning level including the classified exception type and
message. After logging, it SHALL return `Resume` for transient exceptions and
`Resume` for unexpected exceptions, logging the latter at Error level.

The gateway fetch stream and the forecast pipeline SHALL both use this decider.

#### Scenario: Transient exception is logged and resumed
- **WHEN** an `HttpRequestException` occurs in a stream stage using the logging decider
- **THEN** the exception is logged at Warning level
- **THEN** the decider returns `Directive.Resume`

#### Scenario: Unexpected exception is logged and resumed
- **WHEN** an `InvalidOperationException` occurs in a stream stage using the logging decider
- **THEN** the exception is logged at Error level
- **THEN** the decider returns `Directive.Resume`

#### Scenario: Gateway stream survives a fetch failure
- **WHEN** a single `SelectAsyncUnordered` element faults with `HttpRequestException`
- **THEN** that element is dropped, the stream continues, and subsequent fetches succeed
