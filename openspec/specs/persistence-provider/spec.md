# persistence-provider Specification

## Purpose

Configurable persistence backend for Akka.NET journal and snapshot store, using `Akka.Persistence.Sql.Hosting` with typed C# configuration. Supports SQLite (default) and PostgreSQL providers without any HOCON configuration.
## Requirements
### Requirement: Persistence provider is configurable via options
The system SHALL accept a `Njord:Persistence` options section, bound to `PersistenceOptions` (a sealed class), with an optional `ConnectionString` field and a `DataPath` field (default `data/njord.db`). When `ConnectionString` is set, the system SHALL use PostgreSQL (`LinqToDB.ProviderName.PostgreSQL`). When `ConnectionString` is not set, the system SHALL use SQLite (`LinqToDB.ProviderName.SQLiteMS`) with a connection string constructed from the `DataPath`.

#### Scenario: Default provider is SQLite when no ConnectionString
- **WHEN** no `ConnectionString` is configured
- **THEN** the effective provider is SQLite and the connection string is `Data Source={absolute path of DataPath}`

#### Scenario: PostgreSQL selected via ConnectionString
- **WHEN** `Njord:Persistence:ConnectionString` is set to a non-empty value
- **THEN** the effective provider is PostgreSQL and the provided connection string is used

#### Scenario: SQLite with explicit DataPath
- **WHEN** `ConnectionString` is not set and `DataPath` is `custom/path.db`
- **THEN** the resolved connection string is `Data Source={absolute path of custom/path.db}`

### Requirement: SQLite uses DataPath as default connection string
When no `ConnectionString` is configured, the system SHALL construct the SQLite connection string as `Data Source={Path.GetFullPath(DataPath)}` where `DataPath` defaults to `data/njord.db`.

#### Scenario: Default DataPath resolves to absolute path
- **WHEN** no `ConnectionString` and no `DataPath` override
- **THEN** the connection string is `Data Source=` followed by the absolute path of `data/njord.db`

### Requirement: Persistence is configured via WithSqlPersistence without HOCON
The actor system setup SHALL configure journal and snapshot store using `WithSqlPersistence()` from `Akka.Persistence.Sql.Hosting` with the resolved connection string, the mapped LinqToDB provider name, and `autoInitialize: true`. No `AddHocon()` calls SHALL be used for persistence configuration.

#### Scenario: SQLite persistence configured without HOCON
- **WHEN** the actor system starts with SQLite (no ConnectionString)
- **THEN** `WithSqlPersistence` is called with `LinqToDB.ProviderName.SQLiteMS` and the resolved connection string

#### Scenario: PostgreSQL persistence configured without HOCON
- **WHEN** the actor system starts with PostgreSQL (ConnectionString set)
- **THEN** `WithSqlPersistence` is called with `LinqToDB.ProviderName.PostgreSQL` and the provided connection string

#### Scenario: Actor persistence is provider-agnostic
- **WHEN** a `PollWorker` or `BudgetTracker` persists and recovers events
- **THEN** behavior is identical regardless of whether SQLite or PostgreSQL is the configured provider
