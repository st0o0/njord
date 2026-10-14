## MODIFIED Requirements

### Requirement: Persistence provider is configurable via options
The system SHALL accept a `Njord:Persistence` options section, bound to `PersistenceOptions` (a sealed class, separate from `NjordOptions`), with a `Provider` field (enum `PersistenceProvider`: `Sqlite`, `PostgreSql`, default `Sqlite`) and an optional `ConnectionString` field. `AkkaSetupContainer` SHALL map the provider to the `LinqToDB.ProviderName` constant when configuring persistence.

#### Scenario: Default provider is SQLite
- **WHEN** no `Persistence` section is configured
- **THEN** the effective provider is `Sqlite`

#### Scenario: PostgreSQL provider selected
- **WHEN** `Njord:Persistence:Provider` is set to `postgresql`
- **THEN** the effective provider is `PostgreSql`

#### Scenario: Invalid provider value rejected
- **WHEN** `Njord:Persistence:Provider` is set to an unrecognized value
- **THEN** options binding fails with a clear error

### Requirement: SQLite uses PersistencePath as default connection string
When the provider is `Sqlite` and no `ConnectionString` is configured, the system SHALL construct the connection string as `Data Source={full path of StorageOptions.PersistencePath}` (default `data/njord.db`). When an explicit `ConnectionString` is provided, it SHALL be used instead.

#### Scenario: SQLite without explicit connection string
- **WHEN** provider is `Sqlite` and no `ConnectionString` is set
- **THEN** the resolved connection string is `Data Source=` followed by the absolute path of `data/njord.db` (the default PersistencePath)

#### Scenario: SQLite with explicit connection string
- **WHEN** provider is `Sqlite` and `ConnectionString` is `Data Source=custom.db`
- **THEN** the resolved connection string is `Data Source=custom.db`

### Requirement: Persistence is configured via WithSqlPersistence without HOCON
The actor system setup SHALL configure journal and snapshot store using `WithSqlPersistence()` from `Akka.Persistence.Sql.Hosting` with the resolved connection string, the mapped LinqToDB provider name, and `autoInitialize: true`. No `AddHocon()` calls SHALL be used for persistence configuration.

#### Scenario: SQLite persistence configured without HOCON
- **WHEN** the actor system starts with provider `Sqlite`
- **THEN** `WithSqlPersistence` is called with `LinqToDB.ProviderName.SQLiteMS` and the resolved connection string

#### Scenario: PostgreSQL persistence configured without HOCON
- **WHEN** the actor system starts with provider `PostgreSql`
- **THEN** `WithSqlPersistence` is called with `LinqToDB.ProviderName.PostgreSQL` and the resolved connection string

#### Scenario: Actor persistence is provider-agnostic
- **WHEN** `PollScheduler` or `BudgetTracker` persists and recovers events
- **THEN** behavior is identical regardless of whether SQLite or PostgreSQL is the configured provider
