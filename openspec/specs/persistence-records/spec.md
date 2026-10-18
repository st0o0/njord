# persistence-records Specification

## Purpose

Record style and type conventions for Akka.NET persistence events and snapshots, following FunkArr patterns.
## Requirements
### Requirement: Persistence records use positional record style
All persistence event and snapshot records SHALL be positional records
(constructor parameters) without `[JsonProperty]` attributes. This matches
the FunkArr convention and eliminates init-property boilerplate.

#### Scenario: Event record is positional
- **GIVEN** the `ApiCallRecorded` persistence event
- **WHEN** its declaration is inspected
- **THEN** it is a positional record `ApiCallRecorded(int Weight, DateTimeOffset Utc)`

#### Scenario: Snapshot record is positional
- **GIVEN** the `PersistedBudgetTrackerState` snapshot
- **WHEN** its declaration is inspected
- **THEN** it is a positional record with no `[JsonProperty]` attributes

### Requirement: Persistence records use DateTimeOffset and TimeSpan
Persistence records SHALL use `DateTimeOffset` for timestamp fields and
`TimeSpan` for duration fields, not `long` ticks. Newtonsoft serializes
both types natively.

#### Scenario: Event timestamp is DateTimeOffset
- **GIVEN** a `ForecastChanged` event
- **WHEN** its `Utc` field is inspected
- **THEN** it is `DateTimeOffset` (not `long`)

#### Scenario: Poll worker snapshot timestamps are DateTimeOffset
- **GIVEN** a `PersistedPollWorkerState` record
- **WHEN** its change-tracking fields are inspected
- **THEN** `LastChangeUtc` and `PrevChangeUtc` are `DateTimeOffset?`, `NextPollUtc` is `DateTimeOffset`, and `Cycle` (`TimeSpan?`) and `DiscoveryInterval` (`TimeSpan`) are durations

#### Scenario: Serialized timestamp is ISO-8601
- **GIVEN** an `ApiCallRecorded` event with `Utc = 2026-07-15T10:00:00Z`
- **WHEN** serialized by Newtonsoft
- **THEN** the JSON contains an ISO-8601 string, not a numeric ticks value

### Requirement: Persisted types are self-contained
Every type in `Njord.Persistence` SHALL depend only on `System` types and other `Njord.Persistence` types. Closed sets stored in a persisted record SHALL be `Persisted*` enums declared in `Njord.Persistence` with explicit numeric values. `Njord.Persistence` SHALL NOT declare an enum whose name does not start with `Persisted`.

#### Scenario: Poll phase is a persisted enum
- **WHEN** `PersistedPollWorkerState` is inspected
- **THEN** its `Phase` property is `PersistedPollPhase`, and `Discovery` is 0 and `Steady` is 1

#### Scenario: Domain type leaks into a persisted record
- **WHEN** a persisted record gains a property of a domain or message type
- **THEN** the architecture spec fails

### Requirement: Persisted enum conversions are explicit switches
Conversions between a domain enum and its persisted enum SHALL be `ToPersistence()` and `ToDomain()` extension methods implemented as exhaustive switch expressions with a default that throws `ArgumentOutOfRangeException`. Casts between the enums and integer storage SHALL NOT be used.

#### Scenario: Every poll phase roundtrips
- **WHEN** each `PollPhase` is converted with `ToPersistence()` and back with `ToDomain()`
- **THEN** the original phase is returned and an undefined value throws `ArgumentOutOfRangeException`

#### Scenario: Wire format is unchanged
- **WHEN** a `PersistedPollWorkerState` is serialized
- **THEN** the JSON equals the existing golden master with `"Phase": 1`, and an old-format document deserializes into the same state

### Requirement: Persistence mappers live outside the state types
Mapping between runtime state and persisted records SHALL be extension methods in a `*PersistenceMappings` class in the project that owns the state (`GetPersistenceState`, `FromPersistence`, `FromPersistedEvent`). State types SHALL contain only state and transition logic. Conversions of state to message contracts (`PollStateEntry`, `BudgetUsageResult`) SHALL be mapping extensions, not members of the state file.

#### Scenario: State roundtrip through persistence
- **WHEN** a `PollWorkerState` is mapped with `GetPersistenceState()` and back with `FromPersistence()`
- **THEN** the resulting state equals the original in every field

#### Scenario: Budget state after a day change
- **WHEN** a persisted budget state of an earlier day is mapped with `FromPersistence(now)`
- **THEN** the daily counter is reset and the monthly counter is kept, as before

