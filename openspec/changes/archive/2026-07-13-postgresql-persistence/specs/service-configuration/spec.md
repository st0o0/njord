# service-configuration Delta Specification

## ADDED Requirements

### Requirement: Persistence options section is part of NjordOptions
`NjordOptions` SHALL include a `Persistence` property of type `PersistenceOptions` with defaults (`Provider = Sqlite`, `ConnectionString = null`). The existing `PersistencePath` property SHALL remain as the convenience default for SQLite file path.

#### Scenario: Default persistence options
- **WHEN** no `Persistence` section is configured
- **THEN** `NjordOptions.Persistence.Provider` is `Sqlite` and `Persistence.ConnectionString` is null

#### Scenario: PersistencePath coexists with Persistence section
- **WHEN** both `PersistencePath` and `Persistence:Provider` are configured
- **THEN** both values are available; `PersistencePath` is used as fallback only when provider is `Sqlite` and no explicit `ConnectionString` is set

### Requirement: Startup validation covers persistence configuration
The `NjordOptionsValidator` SHALL validate the persistence configuration: `Provider` must be a valid `PersistenceProvider` enum value, and `PostgreSql` provider SHALL require a non-empty `ConnectionString`. Validation failure messages SHALL name the specific problem and suggest corrective action.

#### Scenario: Valid SQLite config passes validation
- **WHEN** provider is `Sqlite` with default settings
- **THEN** validation succeeds

#### Scenario: PostgreSQL without connection string fails validation
- **WHEN** provider is `PostgreSql` and `ConnectionString` is null or empty
- **THEN** validation fails with message indicating PostgreSQL requires `Njord:Persistence:ConnectionString`

#### Scenario: Valid PostgreSQL config passes validation
- **WHEN** provider is `PostgreSql` and `ConnectionString` is non-empty
- **THEN** validation succeeds
