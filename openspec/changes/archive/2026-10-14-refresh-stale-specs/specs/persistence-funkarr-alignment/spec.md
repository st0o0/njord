## ADDED Requirements

### Requirement: Persistence records have wire-format tests
`Njord.Persistence.Tests/WireFormatSpec.cs` SHALL pin the serialized JSON of the journal events and the budget snapshot (`ApiCallRecorded`, `PersistedDataChanged`, `PersistedBudgetTrackerState`) as inline expected strings, guarding against accidental wire-format changes.

#### Scenario: Wire-format change is caught
- **WHEN** a pinned record's serialized form changes
- **THEN** its wire-format test fails until the developer updates the expected JSON deliberately

## MODIFIED Requirements

### Requirement: Persistence records follow FunkArr naming
All persistence records in `Njord.Persistence` SHALL use FunkArr naming
conventions: snapshot records named `Persisted<Name>State`, event records
named in past tense without a `Dto` or `Event` suffix. An event whose plain
name would collide with a message in `Njord.Messages` SHALL carry the
`Persisted` prefix (`PersistedDataChanged`).

#### Scenario: Snapshot record naming
- **WHEN** a persistence snapshot record exists
- **THEN** it SHALL be named `Persisted<Name>State` (e.g. `PersistedBudgetTrackerState`)

#### Scenario: Event record naming
- **WHEN** a persistence event record exists
- **THEN** it SHALL use past-tense naming without suffix (e.g. `ApiCallRecorded`, `PersistedDataChanged`)

#### Scenario: Wire format preserved after rename
- **WHEN** a persistence record is renamed or reshaped
- **THEN** the wire-format tests in `Njord.Persistence.Tests` fail until the expected JSON is updated deliberately; while the project is 0.x such a break is allowed as a `refactor!:` change

### Requirement: Every persistence record has a roundtrip test
Every persistence record SHALL have a test that serializes and deserializes
it, verifying all fields survive the roundtrip.

#### Scenario: DataChanged roundtrip
- **WHEN** a `PersistedDataChanged` record is serialized and deserialized
- **THEN** all property values SHALL match the original

## REMOVED Requirements

### Requirement: Every persistence record has a Verify wire-format snapshot
**Reason**: The requirement named types, files or behavior that no longer exist; it is restated against the current code.
**Migration**: Superseded by "Persistence records have wire-format tests" in this capability.
