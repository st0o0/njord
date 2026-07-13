## Context

Persistence is currently configured via raw HOCON strings in `NjordActorSystemSetup.cs` using `Akka.Persistence.Sqlite`. This is the only HOCON in the codebase — everything else uses typed C# configuration via Akka.Hosting. The persistence backend is hardcoded to SQLite with a file path from `NjordOptions.PersistencePath`.

The service is not yet deployed, so no persisted data exists and no migration is needed.

## Goals / Non-Goals

**Goals:**
- Eliminate all HOCON from the codebase by using `Akka.Persistence.Sql.Hosting`
- Allow users to choose between SQLite (default) and PostgreSQL as persistence backend
- Preserve the existing `PersistencePath` convenience default for SQLite users
- Keep the change minimal — actor code is provider-agnostic and stays untouched

**Non-Goals:**
- Data migration tooling (no existing deployments)
- Additional providers (SQL Server, MySQL, etc.)
- Aspire orchestration for PostgreSQL containers
- Changes to actor persistence patterns (`ReceivePersistentActor`, `PersistenceId`, snapshots)

## Decisions

### D1: `Akka.Persistence.Sql.Hosting` over provider-specific packages

Use the unified `Akka.Persistence.Sql.Hosting` (1.5.67) instead of separate `Akka.Persistence.PostgreSql` + `Akka.Persistence.Sqlite` packages.

**Why**: One package covers both providers via `LinqToDB.ProviderName`. Single `WithSqlPersistence()` call replaces all HOCON. The unified plugin is the maintained successor to the per-database packages.

**Alternative**: Keep `Akka.Persistence.Sqlite` for SQLite and add `Akka.Persistence.PostgreSql.Hosting` for PostgreSQL. Rejected because it doubles the package surface and still requires HOCON for the SQLite path.

### D2: Config model — `PersistenceOptions` nested in `NjordOptions`

```
Njord:Persistence:Provider          = "sqlite" (default) | "postgresql"
Njord:Persistence:ConnectionString  = (optional for sqlite, required for postgresql)
```

The existing `Njord:PersistencePath` stays as the SQLite file-path convenience default. When `Provider` is `sqlite` and no `ConnectionString` is set, the system builds `Data Source={PersistencePath}`.

**Why**: Minimal config for the common case (SQLite just works with defaults). PostgreSQL users explicitly opt in and provide a connection string. The two-field model is simple and unambiguous.

### D3: Provider enum over free-form string

`PersistenceProvider` is a C# enum (`Sqlite`, `PostgreSql`) rather than a string mapped to `LinqToDB.ProviderName` constants. The mapping to LinqToDB provider names happens internally.

**Why**: Compile-time safety, clear error messages on invalid values, no risk of typos in provider name strings.

### D4: Startup validation rejects invalid combinations

The `NjordOptionsValidator` gains rules:
- `Provider` must be a valid enum value
- `Provider = PostgreSql` + missing `ConnectionString` → fail with actionable message
- `Provider = Sqlite` + missing `ConnectionString` → silently fall back to `Data Source={PersistencePath}`

### D5: `WithSqlPersistence` call replaces HOCON block

In `NjordActorSystemSetup.BuildSystem`:

```csharp
var connectionString = persistence.ConnectionString
    ?? (persistence.Provider == PersistenceProvider.Sqlite
        ? $"Data Source={njordOptions.PersistencePath}"
        : throw new InvalidOperationException("..."));

var providerName = persistence.Provider switch
{
    PersistenceProvider.Sqlite => LinqToDB.ProviderName.SQLite,
    PersistenceProvider.PostgreSql => LinqToDB.ProviderName.PostgreSQL,
};

builder
    .WithSqlPersistence(connectionString, providerName, autoInitialize: true)
    .WithResolvableActors(r => { ... });
```

No `AddHocon()`, no `SqlitePersistence.DefaultConfiguration()`.

## Risks / Trade-offs

- **[Different table schema]** → `Akka.Persistence.Sql` uses a different schema than `Akka.Persistence.Sqlite`. Not a risk now (no deployed data), but would matter if someone had a SQLite journal from a dev run. Documented in appsettings comments.
- **[LinqToDB transitive dependency]** → `Akka.Persistence.Sql` pulls in LinqToDB. Adds ~2 MB to the container but no functional concern.
- **[SQLitePCLRaw pin]** → The existing transitive pin for `SQLitePCLRaw.lib.e_sqlite3` (NU1903 advisory) may need version adjustment after the package swap. Verify during implementation.
