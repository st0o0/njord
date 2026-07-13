## Why

The persistence layer currently uses `Akka.Persistence.Sqlite` configured via raw HOCON strings — the only HOCON in the entire codebase. Replacing it with `Akka.Persistence.Sql.Hosting` eliminates HOCON entirely (typed C# config only) and enables user-selectable persistence providers: SQLite for simple/dev setups, PostgreSQL for production deployments with an existing database server.

## What Changes

- Replace `Akka.Persistence.Sqlite` package with `Akka.Persistence.Sql.Hosting` (unified Linq2Db-based plugin covering SQLite and PostgreSQL via provider selection).
- Replace the `AddHocon()` block in `NjordActorSystemSetup` with a single `WithSqlPersistence()` call — zero HOCON remaining.
- Add `PersistenceOptions` config section (`Njord:Persistence`) with `Provider` (sqlite/postgresql, default sqlite) and optional `ConnectionString`.
- SQLite default behavior preserved: when provider is sqlite and no connection string is set, the existing `PersistencePath` is used as `Data Source={PersistencePath}`.
- PostgreSQL requires an explicit connection string; startup validation rejects missing connection string for postgresql provider.

## Non-goals

- Data migration tooling — njord is not yet deployed, no persisted data exists.
- Additional providers beyond SQLite and PostgreSQL.
- Aspire orchestration for PostgreSQL (separate future change).
- Changes to actor persistence logic (`ForecastHistoryActor` etc.) — they are provider-agnostic.

## Capabilities

### New Capabilities

- `persistence-provider`: Configurable persistence backend selection (SQLite default, PostgreSQL optional) with typed C# configuration via `Akka.Persistence.Sql.Hosting`, eliminating all HOCON.

### Modified Capabilities

- `service-configuration`: Adds `Persistence` options section to `NjordOptions` with provider selection and connection string; extends startup validation to reject invalid provider/connection-string combinations.

## Impact

- **Packages**: Remove `Akka.Persistence.Sqlite` (1.5.39), add `Akka.Persistence.Sql.Hosting` (1.5.67). The transitive pin on `SQLitePCLRaw.lib.e_sqlite3` may need review.
- **Config**: `NjordActorSystemSetup.cs` — rewrite persistence setup (smaller, no HOCON). `NjordOptions.cs` — add `PersistenceOptions` property.
- **Validation**: `NjordOptionsValidator` gains persistence-provider validation rules.
- **Tests**: Existing persistence tests continue to work (SQLite in-memory or file). Add validation tests for the new config section.
- **appsettings.json**: Add `Persistence` section with defaults.
- **No API-budget impact**: This change does not alter polling behavior.
