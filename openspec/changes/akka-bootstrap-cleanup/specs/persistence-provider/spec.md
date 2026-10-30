## MODIFIED Requirements

### Requirement: Persistence provider is configurable via options
The system SHALL accept a `Njord:Persistence` options section, bound to `PersistenceOptions` (a sealed class), with an optional `ConnectionString` field and a `DataPath` field (default `data/njord.db`). When `ConnectionString` is set, the system SHALL use PostgreSQL (`LinqToDB.ProviderName.PostgreSQL`). When `ConnectionString` is not set, the system SHALL use SQLite (`LinqToDB.ProviderName.SQLiteMS`) with a connection string constructed from the `DataPath`.

#### Scenario: Default provider is SQLite when no ConnectionString
- **WHEN** no `ConnectionString` is configured
- **THEN** the effective provider is SQLite and the connection string is `Data Source={absolute path of DataPath}`

#### Scenario: PostgreSQL selected via ConnectionString
- **WHEN** `Njord:Persistence:ConnectionString` is set to a non-empty value
- **THEN** the effective provider is PostgreSQL and the provided connection string is used

#### Scenario: SQLite with explicit connection string
- **WHEN** `ConnectionString` is not set and `DataPath` is `custom/path.db`
- **THEN** the resolved connection string is `Data Source={absolute path of custom/path.db}`

### Requirement: SQLite uses DataPath as default connection string
When no `ConnectionString` is configured, the system SHALL construct the SQLite connection string as `Data Source={Path.GetFullPath(DataPath)}` where `DataPath` defaults to `data/njord.db`.

#### Scenario: Default DataPath resolves to absolute path
- **WHEN** no `ConnectionString` and no `DataPath` override
- **THEN** the connection string is `Data Source=` followed by the absolute path of `data/njord.db`

## REMOVED Requirements

### Requirement: Persistence provider is configurable via options
**Reason**: The `PersistenceProvider` enum (`Sqlite`, `PostgreSql`) and `Provider` property are removed. Detection is now implicit: `ConnectionString` set means PostgreSQL, absent means SQLite. This eliminates `PersistenceProvider.cs`, `PersistenceOptionsValidator.cs`, and `PersistenceSelection.cs`.
**Migration**: Remove `Njord:Persistence:Provider` from configuration. PostgreSQL users already have `ConnectionString` set. SQLite users need no changes.

### Requirement: PostgreSQL requires an explicit connection string
**Reason**: This requirement is absorbed into the new detection logic. When `ConnectionString` is set, PostgreSQL is used. There is no way to select PostgreSQL without providing a connection string.
**Migration**: No action needed -- the constraint is enforced by the new detection model.
