## 1. Remove JournalHealthCheck

- [x] 1.1 Delete `src/Njord/Health/JournalHealthCheck.cs`
- [x] 1.2 Remove `journal` registration from `src/Njord/Configuration/HealthSetupContainer.cs` (the `AddCheck<JournalHealthCheck>("journal", ...)` line)
- [x] 1.3 Delete JournalHealthCheck tests (grep for `JournalHealthCheck` in test projects, remove test classes and any test helpers specific to it)

## 2. Add DataDirectoryHealthCheck

- [x] 2.1 Create `src/Njord/Health/DataDirectoryHealthCheck.cs`: sealed class, primary constructor taking `IOptions<PersistenceOptions>`, `internal static Evaluate(string directoryPath, bool isWritable, string? error)` returning `HealthCheckResult`, `CheckHealthAsync` resolves directory from `PersistenceOptions.DataPath`, writes/deletes `.njord-health-probe`, calls `Evaluate`
- [x] 2.2 Register `DataDirectoryHealthCheck` as `data-directory` in `src/Njord/Configuration/HealthSetupContainer.cs` with `failureStatus: HealthStatus.Unhealthy`, conditionally when `PersistenceOptions.Provider == PersistenceProvider.Sqlite`
- [x] 2.3 Add `DataDirectoryHealthCheckTests` in the appropriate test project: `Evaluate` with writable dir returns Healthy, missing dir returns Unhealthy, unwritable dir returns Unhealthy, verify description contains full path

## 3. Update specs and docs

- [x] 3.1 Sync delta specs to main `openspec/specs/health-checks/spec.md`: remove the journal check requirement, update the feature-owned probes requirement (remove `journal` from check list, add note that Akka.Hosting is sole persistence health source), update data-directory requirement to Unhealthy + Sqlite-only
- [x] 3.2 Update `docs/home-assistant/diagnostics.md` line 27: replace `journal` with Akka persistence checks in the check list, keep `data-directory`

## 4. Validation

- [x] 4.1 `dotnet build src/Njord.slnx`
- [x] 4.2 `dotnet format src/Njord.slnx --verify-no-changes`
- [x] 4.3 Run all test projects (each via `dotnet run --project`)
