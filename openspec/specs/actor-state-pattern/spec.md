## Purpose

Defines the immutable state record pattern for persistent actors: thin-shell actors, Apply() extensions, three-tier persistence (Internal State → Snapshot → Persisted DTO), and state-only unit tests.
## Requirements
### Requirement: Immutable state records for persistent actors
Each persistent actor SHALL have a companion immutable `record` holding its full internal state. The actor SHALL NOT hold mutable fields for persistent state — all persistent state SHALL live in the state record. Business decisions derived from that state SHALL be made by named methods on the state record, not by comparisons or computations written inline in the actor's command handlers.

The state record SHALL expose an `Empty` value as a `static readonly` field, not as a method or factory. If the state's initial value depends on runtime context (e.g., current time), the actor SHALL construct the initial state from `Empty` plus an `Apply` call in its constructor rather than parameterizing `Empty`.

`FromPersistence` SHALL be a `static` method on the state record itself (not an extension method), since it is a factory that constructs state from a persisted snapshot.

#### Scenario: State record is immutable
- **WHEN** a persistent actor processes a command that changes state
- **THEN** the actor creates a new state record instance via an `Apply()` call rather than mutating fields

#### Scenario: Actor is a thin shell
- **WHEN** inspecting a persistent actor's message handlers
- **THEN** each handler delegates business logic to state extension methods and only performs actor-specific operations (Persist, Tell, SaveSnapshot, Become)

#### Scenario: Empty is a static readonly field
- **WHEN** inspecting a state record's `Empty` member
- **THEN** it SHALL be `public static readonly <Name>State Empty = new(...)` with compile-time-constant defaults

#### Scenario: FromPersistence is a static method on the record
- **WHEN** inspecting a state record's `FromPersistence` member
- **THEN** it SHALL be `public static <Name>State FromPersistence(Persisted<Name>State dto, ...)` on the record, not an extension method

### Requirement: Apply extension methods for state transitions
Each state record SHALL have `Apply()` extension methods that accept domain events or commands and return a new state record instance. These methods SHALL be pure functions with no side effects.

For persistent actors, command processing SHALL follow the `ProcessCommand` tuple pattern: a single extension method that accepts a command and returns `(<Name>State State, <EventType> Event)` — the new state and the event to persist. The actor's command handler SHALL destructure this tuple, call `Persist` with the event, and assign the new state in the persist callback.

#### Scenario: Apply produces new state from event
- **WHEN** `BudgetTrackerState.Apply(...)` is called with event data
- **THEN** a new `BudgetTrackerState` is returned with updated counters, and the original state is unchanged

#### Scenario: Apply is testable without ActorSystem
- **WHEN** testing state transition logic
- **THEN** tests call `Apply()` directly on state records without creating an ActorSystem, TestKit, or actor instance

#### Scenario: ProcessCommand returns state and event tuple
- **WHEN** `BudgetTrackerState.ProcessCommand(RecordApiCall)` is called
- **THEN** it SHALL return a tuple of (new state, `ApiCallRecorded` persistence event)

### Requirement: GetSnapshot for caller-facing state
Each state record SHALL expose a `GetSnapshot()` extension method that returns a caller-facing view (the response record sent to query senders). This separates internal state shape from the published query response.

#### Scenario: GetSnapshot produces query response
- **WHEN** `BudgetTrackerState.GetSnapshot()` is called
- **THEN** it returns a `BudgetUsageResult` record suitable for sending to the query sender

### Requirement: State-only unit tests
Each state record SHALL have dedicated unit tests that verify `Apply()`, `GetSnapshot()`, `GetPersistenceState()`, and `FromPersistence()` as pure functions without any actor infrastructure.

#### Scenario: Pure function test coverage
- **WHEN** the test suite runs
- **THEN** each state record has tests covering: initial state, applying each event type, snapshot production, persistence roundtrip, and edge cases (month rollover for budget, empty dict for snapshots)

### Requirement: Persistence delete-failures are logged
Persistent actors that call `DeleteMessages`/`DeleteSnapshots` as part of their snapshot
cleanup SHALL register handlers for `DeleteMessagesFailure` and `DeleteSnapshotsFailure`
that log a warning, in addition to the existing `DeleteMessagesSuccess`/
`DeleteSnapshotSuccess` handlers.

#### Scenario: Journal cleanup failure is logged, not dropped
- **WHEN** a `DeleteMessagesFailure` or `DeleteSnapshotsFailure` message is received by
  `BudgetTracker` or by a `PollWorker`
- **THEN** the actor logs a warning including the failure cause instead of letting the
  message reach dead letters unhandled

### Requirement: Three-tier persistence model for every persistent actor
Each persistent actor SHALL maintain three distinct state tiers: Internal State (the immutable state record with full domain logic), Snapshot (caller-facing view via `GetSnapshot()`), and Persisted (journal DTO via `GetPersistenceState()`). The `GetPersistenceState()` method SHALL produce the extend-only DTO type.

Every persistent actor SHALL implement `GetPersistenceState()` and use it in `SaveSnapshot`. Every persistent actor SHALL implement `FromPersistence()` and use it in the `Recover<SnapshotOffer>` handler. No-op snapshot recovery handlers are not permitted.

#### Scenario: GetPersistenceState produces existing DTO
- **WHEN** `BudgetTrackerState.GetPersistenceState()` is called
- **THEN** it returns a `PersistedBudgetTrackerState` matching the existing DTO schema

#### Scenario: FromPersistence restores state from DTO
- **WHEN** an actor recovers from a `SnapshotOffer` containing a persisted state DTO
- **THEN** `<Name>State.FromPersistence(dto)` produces a valid state record, and the actor resumes with correct values

#### Scenario: Persistence roundtrip preserves state
- **WHEN** a state record is converted via `GetPersistenceState()` and then restored via `FromPersistence()`
- **THEN** the resulting state record is equal to the original

#### Scenario: PollWorker implements real snapshot recovery
- **WHEN** a `PollWorker` receives a `SnapshotOffer` during recovery
- **THEN** it SHALL restore state via `PollWorkerState.FromPersistence()`, including the next poll time, instead of ignoring the snapshot

#### Scenario: PollWorker snapshot answers the manager
- **WHEN** a `PollWorker` reports to the manager
- **THEN** the report carries `PollWorkerState.GetSnapshot(target)`, a `PollStateEntry`, never the state record itself

### Requirement: Transient runtime state stays on the actor class
For actors with transient runtime state (actor refs, request in flight, retry counters, pending timers, aggregated views of other actors), those fields SHALL remain as mutable fields on the actor class. Only persistent domain state moves to the state record.

#### Scenario: PollWorker transient fields
- **WHEN** `PollWorker` is inspected
- **THEN** the id of the request in flight and the poll timer are actor fields, while target, discovery interval, started flag and the poll state live in `PollWorkerState`

#### Scenario: PollManager transient fields
- **WHEN** `PollManager` is inspected
- **THEN** the latest reported poll state per target, the per-location poll-cycle tracker, the watched workers and the restart attempts are actor fields; the manager persists nothing

