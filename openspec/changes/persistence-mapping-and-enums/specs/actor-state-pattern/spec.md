## MODIFIED Requirements

### Requirement: Immutable state records for persistent actors
Each persistent actor SHALL have a companion immutable `record` holding its full internal state. The actor SHALL NOT hold mutable fields for persistent state — all persistent state SHALL live in the state record. Business decisions derived from that state SHALL be made by named methods on the state record, not by comparisons or computations written inline in the actor's command handlers.

The state record SHALL expose an `Empty` value as a `static readonly` field, not as a method or factory. If the state's initial value depends on runtime context (e.g., current time), the actor SHALL construct the initial state from `Empty` plus an `Apply` call in its constructor rather than parameterizing `Empty`.

`FromPersistence` and `GetPersistenceState` SHALL be extension methods in a `*PersistenceMappings` class next to the state record (not members of the state record), so the state file holds only state and transitions.

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
- **WHEN** inspecting how a persisted state is converted to a state record
- **THEN** `FromPersistence` SHALL be an extension method on `Persisted<Name>State` in the `*PersistenceMappings` class, not a member of the state record
