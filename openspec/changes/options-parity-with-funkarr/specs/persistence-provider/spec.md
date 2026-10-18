## MODIFIED Requirements

### Requirement: SQLite uses PersistencePath as default connection string
When the provider is `Sqlite` and no `ConnectionString` is configured, the system SHALL construct the connection string as `Data Source={full path of StorageOptions.PersistencePath}` (default `data/njord.db`, configured at `Njord:Storage:PersistencePath`). When an explicit `ConnectionString` is provided, it SHALL be used instead.

#### Scenario: SQLite without explicit connection string
- **WHEN** provider is `Sqlite` and no `ConnectionString` is set
- **THEN** the resolved connection string is `Data Source=` followed by the absolute path of `data/njord.db` (the default PersistencePath)

#### Scenario: SQLite with explicit connection string
- **WHEN** provider is `Sqlite` and `ConnectionString` is `Data Source=custom.db`
- **THEN** the resolved connection string is `Data Source=custom.db`
