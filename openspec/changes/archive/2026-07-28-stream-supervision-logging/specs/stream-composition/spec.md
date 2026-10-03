## MODIFIED Requirements

### Requirement: Stream supervision uses a logging decider
All stream graphs in the system SHALL use the shared `StreamSupervision.LoggingDecider` instead of a blanket `_ => Directive.Resume`. Each call site SHALL pass its own `ILogger` instance so that log entries identify the originating actor or component.

#### Scenario: Stream exception is logged with actor context
- **WHEN** a stream exception occurs in any graph
- **THEN** the exception is logged at Warning level via the actor's ILogger

#### Scenario: Transient errors do not terminate the stream
- **WHEN** an AskTimeoutException occurs in a stream graph
- **THEN** the stream continues processing subsequent elements

#### Scenario: Unexpected errors stop the stream stage
- **WHEN** a NullReferenceException occurs in a stream graph
- **THEN** the stream stage stops, triggering actor supervision
