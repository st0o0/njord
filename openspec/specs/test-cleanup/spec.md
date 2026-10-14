## Purpose

Rules for removing dead test infrastructure, redundant wiring tests, and ensuring Tests.Shared hygiene.
## Requirements
### Requirement: No empty leftover project directories
The repository SHALL NOT contain project directories that are not referenced by `Njord.slnx` and contain no source files. Stale directories from previous restructurings SHALL be deleted.

#### Scenario: Empty directories removed
- **WHEN** the cleanup is complete
- **THEN** `src/Njord.Integration.Tests/`, `src/Njord.IntegrationTests/`, `src/Njord.E2E.Tests/`, `src/Njord.Ingest/`, `src/Njord.Egress/` and `src/Njord.Enrichment/` do not exist on disk, and `src/Njord.Tests/` exists only as the host test project listed in `Njord.slnx`

### Requirement: Tests.Shared holds only helpers with several consumers
Every file in `Njord.Tests.Shared` SHALL be referenced by at least two test projects. Single-consumer helpers SHALL live in their sole consumer.

#### Scenario: PersistenceRoundtripHelper stays local
- **WHEN** `Njord.Tests.Shared` is inspected
- **THEN** `PersistenceRoundtripHelper` is not there; it lives in `Njord.Persistence.Tests`

