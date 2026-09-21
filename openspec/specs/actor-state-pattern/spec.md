## Purpose

Defines the immutable state record pattern for persistent actors: thin-shell actors, Apply() extensions, three-tier persistence (Internal State → Snapshot → Persisted DTO), and state-only unit tests.

## Requirements

### Requirement: Immutable state records for persistent actors
Each persistent actor (BudgetTrackerActor, SchedulerActor, ForecastSnapshotActor, EnrichmentSnapshotActor, ForecastHistoryActor) SHALL have a companion immutable `record` holding its full internal state. The actor SHALL NOT hold mutable fields for persistent state — all persistent state SHALL live in the state record.

#### Scenario: State record is immutable
- **WHEN** a persistent actor processes a command that changes state
- **THEN** the actor creates a new state record instance via an `Apply()` call rather than mutating fields

#### Scenario: Actor is a thin shell
- **WHEN** inspecting a persistent actor's message handlers
- **THEN** each handler delegates business logic to state extension methods and only performs actor-specific operations (Persist, Tell, SaveSnapshot, Become)

### Requirement: Apply extension methods for state transitions
Each state record SHALL have `Apply()` extension methods that accept domain events or commands and return a new state record instance. These methods SHALL be pure functions with no side effects.

#### Scenario: Apply produces new state from event
- **WHEN** `BudgetTrackerState.Apply(ApiCallRecorded)` is called with a recorded API call event
- **THEN** a new `BudgetTrackerState` is returned with updated counters, and the original state is unchanged

#### Scenario: Apply is testable without ActorSystem
- **WHEN** testing state transition logic
- **THEN** tests call `Apply()` directly on state records without creating an ActorSystem, TestKit, or actor instance

### Requirement: GetSnapshot for caller-facing state
Each state record SHALL expose a `GetSnapshot()` extension method that returns a caller-facing view (the response record sent to query senders). This separates internal state shape from the published query response.

#### Scenario: GetSnapshot produces query response
- **WHEN** `BudgetTrackerState.GetSnapshot()` is called
- **THEN** it returns a `BudgetUsageResult` record suitable for sending to the query sender

### Requirement: Three-tier persistence model
Each persistent actor SHALL maintain three distinct state tiers: Internal State (the immutable state record with full domain logic), Snapshot (caller-facing view via `GetSnapshot()`), and Persisted (journal DTO via `GetPersistenceState()`). The `GetPersistenceState()` method SHALL produce the existing extend-only DTO type.

#### Scenario: GetPersistenceState produces existing DTO
- **WHEN** `BudgetTrackerState.GetPersistenceState()` is called
- **THEN** it returns a `BudgetTrackerSnapshotDto` matching the existing DTO schema

#### Scenario: FromPersistence restores state from DTO
- **WHEN** an actor recovers from a `SnapshotOffer` containing a `BudgetTrackerSnapshotDto`
- **THEN** `BudgetTrackerState.FromPersistence(dto)` produces a valid state record, and the actor resumes with correct values

#### Scenario: Persistence roundtrip preserves state
- **WHEN** a state record is converted via `GetPersistenceState()` and then restored via `FromPersistence()`
- **THEN** the resulting state record is semantically equal to the original

### Requirement: Transient state stays on actor
For actors with transient runtime state (queue refs, Become phase, retry counters), those fields SHALL remain as mutable fields on the actor class. Only persistent state moves to the state record.

#### Scenario: SchedulerActor transient fields
- **WHEN** SchedulerActor is refactored
- **THEN** the queue reference, pipeline ref, retry count, and Become phase remain as mutable actor fields while the poll-state dictionary moves to the state record

### Requirement: State-only unit tests
Each state record SHALL have dedicated unit tests that verify `Apply()`, `GetSnapshot()`, `GetPersistenceState()`, and `FromPersistence()` as pure functions without any actor infrastructure.

#### Scenario: Pure function test coverage
- **WHEN** the test suite runs
- **THEN** each state record has tests covering: initial state, applying each event type, snapshot production, persistence roundtrip, and edge cases (month rollover for budget, empty dict for snapshots)
