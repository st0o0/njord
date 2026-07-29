# persistence-backoff Specification

## Purpose

Ensures all persistent actors are supervised with exponential backoff to handle journal failures gracefully.

## Requirements

### Requirement: Persistent actors are supervised with exponential backoff
All `ReceivePersistentActor` instances (SchedulerActor, ForecastHistoryActor, BudgetTrackerActor, ForecastSnapshotActor, EnrichmentSnapshotActor) SHALL be wrapped in a `BackoffSupervisor` using `Backoff.OnFailure` with min backoff 3 seconds, max backoff 30 seconds, and random factor 0.2. The BackoffSupervisor SHALL be the actor registered in the `ActorRegistry`, not the raw persistent actor.

#### Scenario: Persistent actor recovers from journal failure
- **WHEN** a persistent actor's journal write throws an exception
- **THEN** the BackoffSupervisor restarts the actor after the configured backoff delay

#### Scenario: Backoff increases exponentially
- **WHEN** a persistent actor fails repeatedly
- **THEN** the restart delay increases exponentially from 3s up to 30s with ±20% jitter

#### Scenario: Actor registry resolves BackoffSupervisor
- **WHEN** another actor resolves a persistent actor via GetActorAsync
- **THEN** it receives the BackoffSupervisor ref which forwards messages to the child
