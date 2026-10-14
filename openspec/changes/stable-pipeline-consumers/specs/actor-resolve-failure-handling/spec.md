## MODIFIED Requirements

### Requirement: Resolve failures SHALL be logged and retried

When a dependency resolution `PipeTo` delivers a failure record during
**runtime recovery** (after `Terminated`), the actor SHALL log the error at
Warning level and schedule a retry using `BackoffPolicy.DelayWithJitter()`.
The `StreamConsumerActor` base resolves the pipeline asynchronously on start as
well, bounded by its reply timeout, so a consumer never crashes on a missing
registry entry.

On **initial startup**, other actors resolve their dependencies synchronously
(`Context.GetActor<T>()`). A missing registry key throws
`MissingActorRegistryEntryException`, which crashes the actor. For actors behind
`BackoffSupervisor`, the supervisor restarts the child with backoff. For plain
actors, the exception propagates to the parent supervisor. This is correct
because a missing registry key at startup is a wiring bug, not a transient
failure.

#### Scenario: StreamConsumerActor subclass receives a resolve-failed message during recovery
- **WHEN** the `StreamConsumerActor` base cannot resolve the pipeline, or gets no SourceRef within its reply timeout
- **THEN** the actor logs a warning and attaches again after the next backoff delay

#### Scenario: Missing dependency at startup crashes the actor
- **WHEN** an actor calls `Context.GetActor<T>()` during construction and the key is not registered
- **THEN** the actor throws `MissingActorRegistryEntryException` and fails to start

#### Scenario: BackoffSupervisor restarts crashed actor
- **WHEN** an actor behind `BackoffSupervisor` crashes during startup due to a missing registry key
- **THEN** the `BackoffSupervisor` restarts it with exponential backoff, giving the registry time to populate
