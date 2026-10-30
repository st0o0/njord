## Why

The custom `JournalHealthCheck` duplicates what Akka.Hosting's `journal.WithHealthCheck()` already provides, but checks the underlying storage independently of Akka's actual persistence state -- the two can diverge. The spec and docs reference a `data-directory` check that was never implemented. Aligning with FunkArr's pattern (rely on Akka.Hosting for persistence health, custom checks only for things Akka doesn't cover) simplifies the code and closes the spec/docs gap.

## What Changes

- Remove `JournalHealthCheck` and its registration -- Akka.Hosting's persistence health checks (already registered via `WithHealthCheck()` on journal and snapshot builders) cover this
- Remove `JournalHealthCheck` tests
- Add `DataDirectoryHealthCheck` for SQLite data directory writability at runtime (FunkArr pattern: path selector, `Directory.Exists` + write probe)
- Register the new check as `data-directory` with `failureStatus: Unhealthy` (only when Provider == Sqlite)
- Update the health-checks main spec to remove the custom journal check requirement and fix the data-directory requirement
- Update `docs/home-assistant/diagnostics.md` to list the actual checks

## Non-goals

- No changes to polling, budget, or pipeline health checks -- those are correct
- No MQTT health check changes (MQTT is archived/removed)
- No new option validators -- existing Host options (`PersistenceOptions`) already have validators
- No budget impact -- this change does not alter polling or API calls

## Capabilities

### New Capabilities

- `data-directory-health`: Runtime health check for SQLite data directory writability

### Modified Capabilities

- `health-checks`: Remove custom journal check requirement, rely on Akka.Hosting persistence checks, fix data-directory requirement to check SQLite path with Unhealthy status

## Impact

- `src/Njord/Health/JournalHealthCheck.cs` -- removed
- `src/Njord/Configuration/HealthSetupContainer.cs` -- remove journal registration, add data-directory registration
- `src/Njord.Core/Health/DataDirectoryHealthCheck.cs` -- new file (or Host, depending on design)
- `openspec/specs/health-checks/spec.md` -- updated
- `docs/home-assistant/diagnostics.md` -- updated
- Tests: remove JournalHealthCheck tests, add DataDirectoryHealthCheck tests
