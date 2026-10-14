# persistence-record-hygiene Specification

## Purpose

Conventions for the persistence records in `Njord.Persistence`: sealed immutable positional records, no dead versioning infrastructure, one file per type in domain subdirectories.

## Requirements
### Requirement: Persistence DTOs are sealed immutable records
All persistence records in `Njord.Persistence` SHALL be `sealed` positional records (values set only through the constructor, which Newtonsoft.Json uses for deserialization), not mutable classes with public setters.

#### Scenario: DTO immutability after deserialization
- **WHEN** a persistence DTO is deserialized from the journal or a snapshot
- **THEN** its properties cannot be modified after construction — no public setter allows accidental post-deserialization mutation

### Requirement: No dead versioning infrastructure
Persistence DTOs SHALL NOT carry a `Version` property unless recovery code in the
corresponding `FromPersistence`/`ToDomain` mapping actually inspects it and handles
version-specific logic. A `Version` field that is always `1` and never checked is dead
code.

#### Scenario: Version field is either used or absent
- **WHEN** any persistence DTO is inspected
- **THEN** either (a) it has no `Version` property, or (b) its `FromPersistence`/`ToDomain`
  mapping contains an explicit `switch`/`if` on the version value

### Requirement: File-per-type and domain subdirectories
Each root-level persistence record SHALL live in its own file named after it; a record that only exists as an element of another (an element record nested in a snapshot) MAY share that file. Files SHALL be organized into domain subdirectories under `Events/` (`Events/Budget/`, `Events/Polling/`).

#### Scenario: DTO discoverability
- **WHEN** looking for a specific record (e.g. `PersistedBudgetTrackerState`)
- **THEN** it lives in `Events/Budget/PersistedBudgetTrackerState.cs`, not in a multi-type file

