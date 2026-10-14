## MODIFIED Requirements

### Requirement: Persistent actors are supervised with exponential backoff
The persistent singleton `BudgetTracker` SHALL be wrapped in a backoff-on-failure supervisor (`BackoffOnFailure`) with min backoff 3 seconds, max backoff 30 seconds, random factor 0.2 and unlimited retries; the supervisor SHALL be the actor registered as the cluster singleton in the `ActorRegistry`, not the raw persistent actor. The persistent `PollWorker` entities SHALL be supervised by their shard (restart on failure, persisted state recovered); a worker that stops because a journal write or its recovery failed SHALL be started again by `PollManager` after a jittered backoff (1 s up to 30 s, see `poll-scheduler`). `PollManager` itself SHALL be a cluster singleton behind `BackoffOnFailure` with the same 3 s / 30 s / 0.2 / unlimited settings.

#### Scenario: Persistent actor recovers from journal failure
- **WHEN** a persistent actor's journal write throws an exception
- **THEN** the BudgetTracker is restarted by its backoff supervisor after the configured delay, and a poll worker is started again by the poll manager after its backoff

#### Scenario: Backoff increases exponentially
- **WHEN** a persistent actor fails repeatedly
- **THEN** the restart delay increases exponentially (BudgetTracker from 3s up to 30s with ±20% jitter, a poll worker from 1s up to 30s with jitter)

#### Scenario: Actor registry resolves BackoffSupervisor
- **WHEN** another actor resolves the BudgetTracker or the PollManager via GetActorAsync
- **THEN** it receives the singleton proxy whose BackoffSupervisor forwards messages to the child
