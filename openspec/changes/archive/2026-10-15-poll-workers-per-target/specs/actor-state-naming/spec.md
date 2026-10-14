## MODIFIED Requirements

### Requirement: State records match actor name

An actor's state record SHALL be named `<ActorName>State` and its extensions
`<ActorName>StateExtensions`.

#### Scenario: PollScheduler state
- **WHEN** looking up the state record of the persistent poll actor, `PollWorker`
- **THEN** it SHALL be named `PollWorkerState` with extensions in `PollWorkerStateExtensions`
