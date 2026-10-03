## ADDED Requirements

### Requirement: PipeTo calls SHALL provide a failure mapper

Every `PipeTo` call on a `GetActorAsync` or `Task.WhenAll` task SHALL provide a
`failure:` parameter that maps the exception to a project-owned failure record.
The actor MUST NOT rely on Akka's implicit `Status.Failure` wrapping.

#### Scenario: GetActorAsync fails during dependency resolution
- **WHEN** `GetActorAsync<IXxxActor>()` throws (e.g., registry not yet populated)
- **THEN** the actor receives a private `XxxResolveFailed(Exception Cause)` record instead of `Status.Failure`

#### Scenario: Task.WhenAll fails during multi-dependency resolution
- **WHEN** one of the tasks in a `Task.WhenAll` call faults
- **THEN** the actor receives a private resolve-failed record instead of `Status.Failure`

### Requirement: Resolve failures SHALL be logged and retried

When a dependency-resolution `PipeTo` delivers a failure record, the actor SHALL
log the error at Warning level and schedule a retry using the existing
`ScheduleRetryResolve()` mechanism (exponential backoff via `RetryBackoff`).

#### Scenario: StreamConsumerActor subclass receives a resolve-failed message
- **WHEN** a `StreamConsumerActor` subclass receives its resolve-failed record in `ConfigureWaitingForRefs()`
- **THEN** the actor logs a warning including the exception message and calls `ScheduleRetryResolve()`

#### Scenario: PipelineActor receives a resolve-failed message
- **WHEN** `PipelineActor` receives a `SchedulerResolveFailed` in its `Initializing` behavior
- **THEN** the actor logs a warning and schedules a retry resolve via the scheduler

#### Scenario: SchedulerActor receives a resolve-failed message
- **WHEN** `SchedulerActor` receives a `PipelineResolveFailed` in `WaitingForPipeline` or `RestartPipelineConnection`
- **THEN** the actor logs a warning and applies the existing backoff retry logic

### Requirement: Failure records SHALL be private to the actor

Resolve-failed records SHALL be `private sealed record` types defined inside the
actor class, not public messages in `Njord.Messages`. They are internal
implementation details of the resolve flow.

#### Scenario: Resolve-failed records are not visible outside the actor
- **WHEN** inspecting the public API of any affected actor
- **THEN** no resolve-failed record type is visible — they are private nested types
