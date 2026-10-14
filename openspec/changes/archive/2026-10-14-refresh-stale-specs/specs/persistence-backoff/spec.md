## MODIFIED Requirements

### Requirement: Persistent actors are supervised with exponential backoff
Both `ReceivePersistentActor` instances, `PollScheduler` and `BudgetTracker`, SHALL be wrapped in a backoff-on-failure supervisor with min backoff 3 seconds, max backoff 30 seconds, random factor 0.2 and unlimited retries (`PollScheduler` through `BackoffSupervisor.Props(Backoff.OnFailure(...))`, `BudgetTracker` through `BackoffOnFailure`). The supervisor SHALL be the actor registered as the cluster singleton in the `ActorRegistry`, not the raw persistent actor.

#### Scenario: Persistent actor recovers from journal failure
- **WHEN** a persistent actor's journal write throws an exception
- **THEN** the BackoffSupervisor restarts the actor after the configured backoff delay

#### Scenario: Backoff increases exponentially
- **WHEN** a persistent actor fails repeatedly
- **THEN** the restart delay increases exponentially from 3s up to 30s with ±20% jitter

#### Scenario: Actor registry resolves BackoffSupervisor
- **WHEN** another actor resolves a persistent actor via GetActorAsync
- **THEN** it receives the BackoffSupervisor ref which forwards messages to the child
