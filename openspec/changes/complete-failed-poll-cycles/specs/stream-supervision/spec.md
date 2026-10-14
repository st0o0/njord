## MODIFIED Requirements

### Requirement: Shared logging decider for stream supervision
The system SHALL provide a `StreamSupervision.LoggingDecider(ILogger)` static
factory that returns an Akka.Streams `Decider`. Before classifying, the decider
SHALL unwrap an `AggregateException` that has exactly one inner exception,
repeatedly, and classify the innermost single cause; an `AggregateException`
with several inner exceptions SHALL be classified as is. The decider SHALL log
every exception at Warning level including the classified exception type and
message. After logging, it SHALL return `Resume` for transient exceptions and
`Stop` for unexpected exceptions.

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

#### Scenario: Wrapped transient exception is resumed
- **WHEN** an `AggregateException` wrapping a single `TimeoutException` (also nested one level deeper) occurs, as `SelectAsync`/`SelectAsyncUnordered` report a task that was already faulted
- **THEN** the exception is logged at Warning level naming `TimeoutException`
- **THEN** the decider returns `Directive.Resume`

#### Scenario: Wrapped unexpected exception is stopped
- **WHEN** an `AggregateException` wrapping a single `InvalidOperationException` occurs
- **THEN** the decider returns `Directive.Stop`

#### Scenario: Aggregate with several causes is stopped
- **WHEN** an `AggregateException` wrapping a `TimeoutException` and an `HttpRequestException` occurs
- **THEN** the decider returns `Directive.Stop`

#### Scenario: Faulted fetch task in a SelectAsyncUnordered stage resumes the stream
- **WHEN** a `SelectAsyncUnordered` stage using the decider receives an already-faulted task carrying a `TimeoutException` for one element
- **THEN** that element is dropped and the following elements still flow
