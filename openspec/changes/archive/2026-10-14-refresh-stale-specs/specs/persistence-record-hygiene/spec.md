## MODIFIED Requirements

### Requirement: Persistence DTOs are sealed immutable records
All persistence records in `Njord.Persistence` SHALL be `sealed` positional records (values set only through the constructor, which Newtonsoft.Json uses for deserialization), not mutable classes with public setters.

#### Scenario: DTO immutability after deserialization
- **WHEN** a persistence DTO is deserialized from the journal or a snapshot
- **THEN** its properties cannot be modified after construction — no public setter allows accidental post-deserialization mutation

### Requirement: File-per-type and domain subdirectories
Each root-level persistence record SHALL live in its own file named after it; a record that only exists as an element of another (such as `PersistedModelPollEntry` inside `PersistedPollSchedulerState`) MAY share that file. Files SHALL be organized into domain subdirectories under `Events/` (`Events/Budget/`, `Events/Pipeline/`).

#### Scenario: DTO discoverability
- **WHEN** looking for a specific record (e.g. `PersistedBudgetTrackerState`)
- **THEN** it lives in `Events/Budget/PersistedBudgetTrackerState.cs`, not in a multi-type file
