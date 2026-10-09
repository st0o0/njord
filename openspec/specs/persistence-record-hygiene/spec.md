# persistence-record-hygiene Specification

## Purpose
TBD - created by archiving change persistence-messages-cleanup. Update Purpose after archive.
## Requirements
### Requirement: Persistence DTOs are sealed immutable records
All persistence DTOs in `Njord.Persistence` SHALL be `sealed record` types with
`{ get; init; }` properties (for Newtonsoft.Json deserialization compatibility), not
mutable classes with public setters.

#### Scenario: DTO immutability after deserialization
- **WHEN** a persistence DTO is deserialized from the journal or a snapshot
- **THEN** its properties cannot be modified after construction — no public setter allows
  accidental post-deserialization mutation

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
Each root-level persistence DTO SHALL live in its own file. Files SHALL be organized into
domain subdirectories (e.g. `Pipeline/`, `Enrichment/`, `Grpc/`).

#### Scenario: DTO discoverability
- **WHEN** looking for a specific DTO (e.g. `BudgetTrackerSnapshotDto`)
- **THEN** it lives in a file named `BudgetTrackerSnapshotDto.cs` inside a
  domain-specific subdirectory, not in a multi-type `BudgetTrackerDtos.cs`

