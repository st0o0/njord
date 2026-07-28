# stream-supervision Specification

## Purpose

Shared stream supervision decider with logging and type-aware exception handling. Replaces blanket `_ => Directive.Resume` across all stream graphs with a logged, classified decider.

## Requirements

### Requirement: Shared logging decider for stream supervision
The system SHALL provide a `StreamSupervision.LoggingDecider(ILogger)` static
factory that returns an Akka.Streams `Decider`. The decider SHALL log every
exception at Warning level including the exception type and message. After
logging, it SHALL return `Resume` for transient exceptions and `Stop` for
unexpected exceptions.

#### Scenario: Transient exception is logged and resumed
- **WHEN** an `AskTimeoutException` occurs in a stream stage using the logging decider
- **THEN** the exception is logged at Warning level
- **THEN** the decider returns `Directive.Resume`

#### Scenario: Cancellation exception is logged and resumed
- **WHEN** a `TaskCanceledException` or `OperationCanceledException` occurs
- **THEN** the exception is logged at Warning level
- **THEN** the decider returns `Directive.Resume`

#### Scenario: Timeout exception is logged and resumed
- **WHEN** a `TimeoutException` occurs
- **THEN** the exception is logged at Warning level
- **THEN** the decider returns `Directive.Resume`

#### Scenario: HTTP transport exception is logged and resumed
- **WHEN** an `HttpRequestException` occurs
- **THEN** the exception is logged at Warning level
- **THEN** the decider returns `Directive.Resume`

#### Scenario: Unexpected exception is logged and stopped
- **WHEN** a `NullReferenceException` occurs in a stream stage using the logging decider
- **THEN** the exception is logged at Warning level
- **THEN** the decider returns `Directive.Stop`

#### Scenario: Unknown exception type is logged and stopped
- **WHEN** an `InvalidOperationException` occurs
- **THEN** the exception is logged at Warning level
- **THEN** the decider returns `Directive.Stop`
