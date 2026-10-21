## Purpose

Record style, naming, type conventions, layout, mapping, and testing for Akka.NET persistence events and snapshots in `Njord.Persistence`, following FunkArr conventions.

## Requirements

### Requirement: Persistence records use positional record style
All persistence event and snapshot records SHALL be `sealed` positional records (values set only through the constructor) without `[JsonProperty]` attributes or public setters.

#### Scenario: Event record is positional
- **WHEN** any persistence event record is inspected
- **THEN** it is a positional record without `[JsonProperty]` attributes

#### Scenario: DTO immutability after deserialization
- **WHEN** a persistence DTO is deserialized from the journal or a snapshot
- **THEN** its properties cannot be modified after construction

### Requirement: Persistence records follow FunkArr naming
Snapshot records SHALL be named `Persisted<Name>State`, event records in past tense without a `Dto` or `Event` suffix. An event whose plain name would collide with a message in `Njord.Messages` SHALL carry the `Persisted` prefix.

#### Scenario: Snapshot record naming
- **WHEN** a persistence snapshot record exists
- **THEN** it SHALL be named `Persisted<Name>State` (e.g. `PersistedBudgetTrackerState`)

#### Scenario: Event record naming
- **WHEN** a persistence event record exists
- **THEN** it SHALL use past-tense naming without suffix (e.g. `ApiCallRecorded`, `ForecastChanged`, `PollingStarted`)

### Requirement: Persistence records use DateTimeOffset and TimeSpan
Persistence records SHALL use `DateTimeOffset` for timestamp fields and `TimeSpan` for duration fields, not `long` ticks.

#### Scenario: Serialized timestamp is ISO-8601
- **WHEN** an event with a `DateTimeOffset` field is serialized by Newtonsoft
- **THEN** the JSON contains an ISO-8601 string, not a numeric ticks value

### Requirement: Persisted types are self-contained
Every type in `Njord.Persistence` SHALL depend only on `System` types and other `Njord.Persistence` types. Closed sets SHALL be `Persisted*` enums with explicit numeric values.

#### Scenario: Domain type leaks into a persisted record
- **WHEN** a persisted record gains a property of a domain or message type
- **THEN** the architecture spec fails

### Requirement: Persisted enum conversions are explicit switches
Conversions between a domain enum and its persisted enum SHALL be `ToPersistence()` and `ToDomain()` extension methods as exhaustive switch expressions with a default that throws `ArgumentOutOfRangeException`. No casts between enums.

#### Scenario: Every poll phase roundtrips
- **WHEN** each `PollPhase` is converted with `ToPersistence()` and back with `ToDomain()`
- **THEN** the original phase is returned and an undefined value throws

### Requirement: No dead versioning infrastructure
Persistence DTOs SHALL NOT carry a `Version` property unless recovery code actually inspects it.

#### Scenario: Version field is either used or absent
- **WHEN** any persistence DTO is inspected
- **THEN** either it has no `Version` property, or its mapping contains an explicit switch on the version value

### Requirement: File-per-type and domain subdirectories
Each root-level persistence record SHALL live in its own file, organized under `Events/<Domain>/` directories (`Events/Budget/`, `Events/Polling/`).

#### Scenario: DTO discoverability
- **WHEN** looking for `PersistedBudgetTrackerState`
- **THEN** it lives in `Events/Budget/PersistedBudgetTrackerState.cs`

### Requirement: Persistence mappers live outside the state types
Mapping between runtime state and persisted records SHALL be extension methods in `*PersistenceMappings` classes in the owning project. State types contain only state and transition logic.

#### Scenario: State roundtrip through persistence
- **WHEN** a `PollWorkerState` is mapped with `GetPersistenceState()` and back with `FromPersistence()`
- **THEN** the resulting state equals the original

### Requirement: Apply methods take persistence event records
State `Apply` methods SHALL take the persistence event record directly, not decomposed primitive values.

#### Scenario: BudgetTrackerState.Apply takes ApiCallRecorded
- **WHEN** an `ApiCallRecorded` event is applied to `BudgetTrackerState`
- **THEN** the `Apply` method accepts `ApiCallRecorded` (not `int weight, DateTimeOffset now`)

### Requirement: Snapshot scheduling uses LastSequenceNr
Persistent actors SHALL use `LastSequenceNr % SnapshotInterval == 0` for snapshot scheduling. Snapshot counters SHALL NOT exist on domain state records.

#### Scenario: BudgetTracker snapshots by sequence number
- **WHEN** `LastSequenceNr` is divisible by `SnapshotInterval`
- **THEN** it saves a snapshot

### Requirement: Journal cleanup after successful snapshot
Persistent actors SHALL delete old journal entries and snapshots after a successful snapshot save (`DeleteMessages(N)` + `DeleteSnapshots` for entries before N).

#### Scenario: Journal entries are cleaned after snapshot
- **WHEN** a snapshot is saved successfully at sequence number N
- **THEN** the `SaveSnapshotSuccess` handler calls `DeleteMessages(N)` and `DeleteSnapshots`

### Requirement: Every persistence record has a roundtrip test
Every persistence record SHALL have a test that serializes and deserializes it using `JsonSerializerSettings` with `TypeNameHandling.All` (matching Akka.Persistence.Sql), verifying all fields survive.

#### Scenario: Roundtrip preserves all fields
- **WHEN** any persistence record is serialized and deserialized with `TypeNameHandling.All`
- **THEN** all fields have identical values after the roundtrip

### Requirement: Persistence records have wire-format tests
Wire-format tests SHALL pin the serialized JSON of persistence records as Verify baselines or inline expected strings, guarding against accidental wire-format changes.

#### Scenario: Wire-format change is caught
- **WHEN** a pinned record's serialized form changes
- **THEN** its wire-format test fails until the developer updates the expected JSON deliberately


### Requirement: Poll worker records carry location coordinates
`PollingStarted` and `PersistedPollWorkerState` SHALL carry the target location's latitude and longitude as trailing nullable members defaulting to null. The poll worker SHALL persist them, and state recovery (event replay and snapshot restore) SHALL rebuild the target location with them, falling back to `0` when they are null.

#### Scenario: Started worker persists coordinates
- **WHEN** a `PollWorker` handles `StartPolling` for `Location("lucerne", 47.05, 8.31)`
- **THEN** the persisted `PollingStarted` has `Latitude` 47.05 and `Longitude` 8.31

#### Scenario: Recovered worker keeps coordinates
- **WHEN** a `PollWorker` recovers from its journal after having been started with `Location("lucerne", 47.05, 8.31)`
- **THEN** its target location has latitude 47.05 and longitude 8.31

#### Scenario: Snapshot restore keeps coordinates
- **WHEN** a worker state with coordinates 47.05 / 8.31 is mapped to `PersistedPollWorkerState` and back
- **THEN** the restored target location has latitude 47.05 and longitude 8.31

#### Scenario: Old data falls back to zero
- **WHEN** a `PollingStarted` event or snapshot without coordinates (null) is applied or restored
- **THEN** the target location has latitude 0 and longitude 0 and the worker is otherwise unchanged

### Requirement: Started worker refreshes a changed target
A `PollWorker` that is already started SHALL persist a new `PollingStarted` when `StartPolling` arrives with different coordinates, instead of ignoring the command.

#### Scenario: Restart with changed coordinates
- **WHEN** a started worker for `lucerne` at 47.05 / 8.31 receives `StartPolling` for `lucerne` at 47.10 / 8.40
- **THEN** the worker persists `PollingStarted` and its target location has latitude 47.10 and longitude 8.40, with domain poll states preserved

#### Scenario: Restart with identical target
- **WHEN** a started worker receives `StartPolling` with the same coordinates and discovery interval
- **THEN** no event is persisted and the worker reports `Planned`
