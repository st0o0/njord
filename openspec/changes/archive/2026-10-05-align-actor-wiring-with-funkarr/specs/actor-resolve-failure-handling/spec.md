## MODIFIED Requirements

### Requirement: Resolve failures SHALL be logged and retried
When a dependency re-resolution `PipeTo` delivers a failure record during
**runtime recovery** (after `Terminated`), the actor SHALL log the error at
Warning level and schedule a retry using `BackoffPolicy.DelayWithJitter()`.

On **initial startup**, dependency resolution is synchronous
(`Context.GetActor<T>()`). A missing registry key throws
`MissingActorRegistryEntryException`, which crashes the actor. For actors behind
`BackoffSupervisor`, the supervisor restarts the child with backoff. For plain
actors, the exception propagates to the parent supervisor. This is correct
because a missing registry key at startup is a wiring bug, not a transient
failure.

#### Scenario: StreamConsumerActor subclass receives a resolve-failed message during recovery
- **WHEN** a `StreamConsumerActor` subclass receives its resolve-failed record in `ConfigureWaitingForRefs()` during runtime recovery
- **THEN** the actor logs a warning including the exception message and calls `ScheduleRetryResolve()`

#### Scenario: Missing dependency at startup crashes the actor
- **WHEN** an actor calls `Context.GetActor<T>()` during construction and the key is not registered
- **THEN** the actor throws `MissingActorRegistryEntryException` and fails to start

#### Scenario: BackoffSupervisor restarts crashed actor
- **WHEN** an actor behind `BackoffSupervisor` crashes during startup due to a missing registry key
- **THEN** the `BackoffSupervisor` restarts it with exponential backoff, giving the registry time to populate

## REMOVED Requirements

### Requirement: PipeTo calls SHALL provide a failure mapper
**Reason**: Initial dependency resolution is now synchronous
(`Context.GetActor<T>()` in the constructor). The `GetActorAsync` + `PipeTo` +
failure-mapper pattern is only used during runtime recovery
(`StreamConsumerActor` re-resolve after `Terminated`), not at startup. Standalone
actors (`SchedulerActor`, `PipelineActor`) no longer use `GetActorAsync` at all.
**Migration**: Remove private `XxxResolveFailed` records and `PipeTo` failure
mappers from standalone actors. `StreamConsumerActor` subclasses keep their
failure mappers for the runtime recovery path only.

### Requirement: Failure records SHALL be private to the actor
**Reason**: Standalone actors (`SchedulerActor`, `PipelineActor`) no longer have
resolve-failed records — they resolve synchronously. `StreamConsumerActor`
subclasses still have private resolve-failed records for the runtime recovery
path, but this is covered by the existing `StreamConsumerActor` spec, not this
one.
**Migration**: Delete private resolve-failed records from standalone actors.
