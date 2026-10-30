# data-directory-health Specification

## Purpose

Runtime health check for SQLite data directory writability, registered only
when the persistence provider is Sqlite.

## Requirements

### Requirement: Data directory health check for SQLite persistence

A `DataDirectoryHealthCheck` SHALL verify at runtime that the SQLite data directory exists and is writable. The check SHALL be registered as `data-directory` with `failureStatus: HealthStatus.Unhealthy` only when the persistence provider is `Sqlite`. When the provider is `PostgreSql`, no data-directory check SHALL be registered.

The check SHALL resolve the directory from the configured SQLite data path (`PersistenceOptions.DataPath`), attempt to write and delete a probe file, and report `Healthy` with the full directory path on success or `Unhealthy` with the path and error on failure.

The check SHALL delegate its decision to an `internal static Evaluate` method over plain values (directory path, write-probe result) so it is testable without hosting.

#### Scenario: Writable SQLite directory
- **WHEN** the persistence provider is Sqlite and the data directory exists and is writable
- **THEN** the `data-directory` check reports `Healthy` with the full directory path

#### Scenario: Missing SQLite directory
- **WHEN** the persistence provider is Sqlite and the data directory does not exist
- **THEN** the `data-directory` check reports `Unhealthy` naming the full path

#### Scenario: Unwritable SQLite directory
- **WHEN** the persistence provider is Sqlite and the data directory exists but a probe file cannot be written
- **THEN** the `data-directory` check reports `Unhealthy` naming the full path and the error

#### Scenario: PostgreSQL provider skips data-directory check
- **WHEN** the persistence provider is PostgreSql
- **THEN** no `data-directory` health check is registered

#### Scenario: Evaluate without hosting
- **WHEN** the static evaluate method is called with a directory path and a write-probe result
- **THEN** it returns the expected `HealthCheckResult` without a host or actor system
