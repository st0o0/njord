# persistence-provider Specification

## Purpose

Configurable persistence backend for Akka.NET journal and snapshot store, using `Akka.Persistence.Sql.Hosting` with typed C# configuration. Supports SQLite (default) and PostgreSQL providers without any HOCON configuration.

## ADDED Requirements

### Requirement: Persistence provider is configurable via options
The system SHALL accept a `Persistence` options section under `NjordOptions` with a `Provider` field (enum: `Sqlite`, `PostgreSql`, default `Sqlite`) and an optional `ConnectionString` field. The `PersistenceOptions` record SHALL map provider values to `LinqToDB.ProviderName` constants internally.

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
When the provider is `Sqlite` and no `ConnectionString` is configured, the system SHALL construct the connection string as `Data Source={PersistencePath}` using the existing `NjordOptions.PersistencePath` value. When an explicit `ConnectionString` is provided, it SHALL be used instead.

#### Scenario: SQLite without explicit connection string
- **WHEN** provider is `Sqlite` and no `ConnectionString` is set
- **THEN** the resolved connection string is `Data Source=data/njord-journal.db` (the default PersistencePath)

#### Scenario: SQLite with explicit connection string
- **WHEN** provider is `Sqlite` and `ConnectionString` is `Data Source=custom.db`
- **THEN** the resolved connection string is `Data Source=custom.db`

### Requirement: PostgreSQL requires an explicit connection string
When the provider is `PostgreSql`, the system SHALL require a non-empty `ConnectionString`. Startup validation SHALL fail with an actionable message if the connection string is missing.

#### Scenario: PostgreSQL without connection string fails validation
- **WHEN** provider is `PostgreSql` and no `ConnectionString` is set
- **THEN** startup validation fails with a message indicating that PostgreSQL requires a connection string

#### Scenario: PostgreSQL with connection string succeeds
- **WHEN** provider is `PostgreSql` and `ConnectionString` is `Host=localhost;Database=njord;Username=njord;Password=secret`
- **THEN** startup validation succeeds and the connection string is used

### Requirement: Persistence is configured via WithSqlPersistence without HOCON
The actor system setup SHALL configure journal and snapshot store using `WithSqlPersistence()` from `Akka.Persistence.Sql.Hosting` with the resolved connection string, the mapped LinqToDB provider name, and `autoInitialize: true`. No `AddHocon()` calls SHALL be used for persistence configuration.

#### Scenario: SQLite persistence configured without HOCON
- **WHEN** the actor system starts with provider `Sqlite`
- **THEN** `WithSqlPersistence` is called with `LinqToDB.ProviderName.SQLite` and the resolved connection string

#### Scenario: PostgreSQL persistence configured without HOCON
- **WHEN** the actor system starts with provider `PostgreSql`
- **THEN** `WithSqlPersistence` is called with `LinqToDB.ProviderName.PostgreSQL` and the resolved connection string

#### Scenario: Actor persistence is provider-agnostic
- **WHEN** `ForecastHistoryActor` persists and recovers events
- **THEN** behavior is identical regardless of whether SQLite or PostgreSQL is the configured provider
