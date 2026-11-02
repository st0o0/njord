## MODIFIED Requirements

### Requirement: Shared logging decider for stream supervision
The system SHALL use `Terp.Akka`'s `ResumeOnError(ILoggingAdapter)` extension method on `ActorAttributes` (or on a `Flow`/`Source`/`Sink`) to apply a logging stream supervision strategy. The decider unwraps single-cause `AggregateException`s, logs every exception at Warning level, returns `Resume` for transient exceptions (ask timeout, cancellation, timeout, HTTP transport) and `Stop` for unexpected exceptions. The njord-local `StreamSupervision` class SHALL be deleted; its behavior is provided by `Terp.Akka`.

#### Scenario: Transient exception is logged and resumed
- **WHEN** an `AskTimeoutException` occurs in a stream stage using `ResumeOnError`
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
- **WHEN** a `NullReferenceException` occurs in a stream stage using `ResumeOnError`
- **THEN** the exception is logged at Warning level
- **THEN** the decider returns `Directive.Stop`

#### Scenario: Wrapped transient exception is resumed
- **WHEN** an `AggregateException` wrapping a single `TimeoutException` occurs
- **THEN** the exception is logged at Warning level naming `TimeoutException`
- **THEN** the decider returns `Directive.Resume`

#### Scenario: Aggregate with several causes is stopped
- **WHEN** an `AggregateException` wrapping a `TimeoutException` and an `HttpRequestException` occurs
- **THEN** the decider returns `Directive.Stop`

#### Scenario: Faulted fetch task in a SelectAsyncUnordered stage resumes the stream
- **WHEN** a `SelectAsyncUnordered` stage using `ResumeOnError` receives an already-faulted task carrying a `TimeoutException` for one element
- **THEN** that element is dropped and the following elements still flow

## REMOVED Requirements

### Requirement: StreamSupervision class in Njord.Core
**Reason**: `StreamSupervision.LoggingDecider` and `StreamSupervision.IsTransient` are now provided by `Terp.Akka` as `ResumeOnError`. The local class is redundant.
**Migration**: Delete `Njord.Core/Actors/StreamSupervision.cs`. Replace call sites with `.ResumeOnError(log)` (with logger) or `.ResumeOnError()` (without logger, for tests).
