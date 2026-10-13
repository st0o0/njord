## Purpose

Defines how Njord.Persistence aligns with FunkArr conventions: record naming, domain-scoped folder layout, and roundtrip plus Verify wire-format tests for every persistence record.

## Requirements

### Requirement: Persistence records follow FunkArr naming

All persistence records in `Njord.Persistence` SHALL use FunkArr naming
conventions: snapshot records named `Persisted<Name>State`, event records
named in past-tense without suffix. No `Dto` or `Event` suffix on any
persistence record.

`[JsonProperty]` string values MUST remain unchanged to preserve wire
compatibility.

#### Scenario: Snapshot record naming
- **WHEN** a persistence snapshot record exists
- **THEN** it SHALL be named `Persisted<Name>State` (e.g. `PersistedBudgetTrackerState`)

#### Scenario: Event record naming
- **WHEN** a persistence event record exists
- **THEN** it SHALL use past-tense naming without suffix (e.g. `ApiCallRecorded`, `DataChanged`)

#### Scenario: Wire format preserved after rename
- **WHEN** a persistence record is renamed
- **THEN** all `[JsonProperty("...")]` string values SHALL remain identical to their pre-rename values

### Requirement: Persistence project uses domain-scoped layout

Persistence records SHALL be organized under `Events/<Domain>/` directories
within `Njord.Persistence`, matching the FunkArr convention.

#### Scenario: Budget domain records
- **WHEN** looking up budget persistence records
- **THEN** they SHALL be at `Njord.Persistence/Events/Budget/`

#### Scenario: Pipeline domain records
- **WHEN** looking up pipeline persistence records
- **THEN** they SHALL be at `Njord.Persistence/Events/Pipeline/`

### Requirement: Every persistence record has a roundtrip test

Every persistence record SHALL have a test that serializes and deserializes
it, verifying all fields survive the roundtrip.

#### Scenario: DataChanged roundtrip
- **WHEN** a `DataChanged` record is serialized and deserialized
- **THEN** all property values SHALL match the original

### Requirement: Every persistence record has a Verify wire-format snapshot

Every persistence record SHALL have a Verify snapshot test that captures
the serialized JSON form, guarding against accidental wire-format changes.

#### Scenario: New wire-format snapshot
- **WHEN** a persistence record's serialized form changes
- **THEN** the Verify snapshot test SHALL fail until the developer explicitly approves the new baseline
