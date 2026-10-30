## MODIFIED Requirements

### Requirement: Health checks are feature-owned probes with an explicit failure status

Each health check SHALL be a sealed `IHealthCheck` declared in the project that owns the dependency, registered by that project's setup container with an explicit `failureStatus`, and SHALL delegate its decision to an `internal static` method over plain values (state record, `TimeProvider` reading, options) so that it is testable without hosting. Essential dependencies (persistence, pipeline freshness, exhausted budget, data directory) SHALL fail as `Unhealthy`; optional ones (low budget) SHALL fail as `Degraded`. A check SHALL NOT send actor messages.

#### Scenario: Evaluate without hosting

- **WHEN** a spec calls the static evaluate method of a check with a state record and a fake time
- **THEN** it receives the expected status and description without a host or actor system

#### Scenario: Check registration

- **WHEN** the host's services are built
- **THEN** the registered custom checks are `pipeline`, `data-directory` and `budget`
- **AND** Akka.Hosting registers persistence (journal, snapshot), actor system liveness and cluster readiness checks

### Requirement: Akka persistence health checks on journal and snapshot stores

The `AkkaSetupContainer` SHALL configure `WithSqlPersistence` with `journalBuilder` calling `WithHealthCheck()` and `snapshotBuilder` calling `WithHealthCheck()`. These Akka-level checks verify that the persistence layer can perform read/write operations. No additional custom journal health check SHALL be registered -- the Akka.Hosting checks are the single source of truth for persistence health.

#### Scenario: Journal persistence health check is registered

- **WHEN** the actor system starts
- **THEN** an Akka persistence journal health check is registered via `WithHealthCheck()` on the journal builder

#### Scenario: Snapshot persistence health check is registered

- **WHEN** the actor system starts
- **THEN** an Akka persistence snapshot health check is registered via `WithHealthCheck()` on the snapshot builder

### Requirement: Data directory health check

A `data-directory` check SHALL report `Unhealthy` when the SQLite data directory does not exist or is not writable, and `Healthy` otherwise. The check SHALL only be registered when the persistence provider is `Sqlite`.

#### Scenario: Writable SQLite directory

- **WHEN** the persistence provider is Sqlite and the data directory is writable
- **THEN** the check reports `Healthy`

#### Scenario: Unwritable SQLite directory

- **WHEN** the persistence provider is Sqlite and the data directory is not writable
- **THEN** the check reports `Unhealthy` naming the full path

## REMOVED Requirements

### Requirement: Journal health check

**Reason**: The custom `JournalHealthCheck` duplicates what Akka.Hosting's `journal.WithHealthCheck()` already provides. The custom check verifies underlying storage connectivity independently of Akka's persistence state, meaning the two can diverge. Akka.Hosting's checks are authoritative for persistence health.

**Migration**: No migration needed. The Akka.Hosting persistence health checks (`WithHealthCheck()` on journal and snapshot builders) are already registered in `AkkaSetupContainer` and cover persistence health. Remove the custom `JournalHealthCheck` class and its registration.
