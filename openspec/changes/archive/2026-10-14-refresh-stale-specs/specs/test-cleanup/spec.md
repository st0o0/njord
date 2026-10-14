## ADDED Requirements

### Requirement: Tests.Shared holds only helpers with several consumers
Every file in `Njord.Tests.Shared` SHALL be referenced by at least two test projects. Single-consumer helpers SHALL live in their sole consumer.

#### Scenario: PersistenceRoundtripHelper stays local
- **WHEN** `Njord.Tests.Shared` is inspected
- **THEN** `PersistenceRoundtripHelper` is not there; it lives in `Njord.Persistence.Tests`

## MODIFIED Requirements

### Requirement: No empty leftover project directories
The repository SHALL NOT contain project directories that are not referenced by `Njord.slnx` and contain no source files. Stale directories from previous restructurings SHALL be deleted.

#### Scenario: Empty directories removed
- **WHEN** the cleanup is complete
- **THEN** `src/Njord.Integration.Tests/`, `src/Njord.IntegrationTests/`, `src/Njord.E2E.Tests/`, `src/Njord.Ingest/`, `src/Njord.Egress/` and `src/Njord.Enrichment/` do not exist on disk, and `src/Njord.Tests/` exists only as the host test project listed in `Njord.slnx`

## REMOVED Requirements

### Requirement: No stub E2E specs in IntegrationTests
**Reason**: There is no `Njord.IntegrationTests` project; end-to-end verification is the `e2e-test` skill.
**Migration**: See `test-project-structure` ("End-to-end stack verification is a skill, not a dotnet test project").

### Requirement: Redundant wiring tests removed
**Reason**: The project it referred to (`Njord.IntegrationTests`) and the specs it kept (`PersistenceBeforeActorsSpec`, `StreamShutdownTaskSpec`) no longer exist; the coordinated stream shutdown was removed.
**Migration**: None.

### Requirement: Tests.Shared contains only multi-project helpers
**Reason**: The requirement named types, files or behavior that no longer exist; it is restated against the current code.
**Migration**: Superseded by "Tests.Shared holds only helpers with several consumers" in this capability.
