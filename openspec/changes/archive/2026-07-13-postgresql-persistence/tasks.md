## 1. Package swap

- [x] 1.1 Remove `Akka.Persistence.Sqlite` from `src/Njord/Njord.csproj` and `src/Directory.Packages.props`; add `Akka.Persistence.Sql.Hosting` (1.5.67) to both. Review whether the `SQLitePCLRaw.lib.e_sqlite3` transitive pin is still needed.
- [x] 1.2 Run `dotnet build src/Njord.slnx` — expect compile errors in `NjordActorSystemSetup.cs` (the `SqlitePersistence` import). Confirm no other files reference the removed package.

## 2. Configuration model

- [x] 2.1 Create `PersistenceProvider` enum (`Sqlite`, `PostgreSql`) and `PersistenceOptions` record (`Provider`, `ConnectionString`) in `src/Njord/Configuration/`. Add `Persistence` property to `NjordOptions` with default `new PersistenceOptions()`.
- [x] 2.2 Add persistence validation rules to `NjordOptionsValidator` in `src/Njord/Configuration/`: PostgreSQL requires non-empty `ConnectionString`; SQLite allows null (falls back to `PersistencePath`).
- [x] 2.3 Write `PersistenceOptionsValidationSpec` in `src/Njord.Tests/Configuration/`: valid SQLite default, valid PostgreSQL with connection string, PostgreSQL without connection string fails, SQLite with explicit connection string.

## 3. Actor system setup

- [x] 3.1 Rewrite `NjordActorSystemSetup.BuildSystem` in `src/Njord/Configuration/NjordActorSystemSetup.cs`: remove both `AddHocon()` calls, remove `using Akka.Persistence.Sqlite`, resolve connection string (explicit > PersistencePath fallback for SQLite), map `PersistenceProvider` to `LinqToDB.ProviderName`, call `WithSqlPersistence(connectionString, providerName, autoInitialize: true)`.
- [x] 3.2 Add `Persistence` section with defaults to `src/Njord/appsettings.json`.

## 4. Test verification

- [x] 4.1 Run existing persistence tests (`ForecastHistoryActorSpec`) — they must pass unchanged with the new SQLite backend from `Akka.Persistence.Sql`.
- [x] 4.2 Run full test suite and confirm green: `dotnet run --project src/Njord.Tests/Njord.Tests.csproj`

## 5. Validation

```powershell
dotnet build src/Njord.slnx
dotnet run --project src/Njord.Tests/Njord.Tests.csproj
```
