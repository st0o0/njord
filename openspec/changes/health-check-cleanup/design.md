## Context

njord registers Akka.Hosting health checks (`WithHealthCheck()` on journal/snapshot, `WithActorSystemLivenessCheck()`, `WithAkkaClusterReadinessCheck()`) in `AkkaSetupContainer`. These feed into ASP.NET's health check pipeline automatically. Additionally, a custom `JournalHealthCheck` in `src/Njord/Health/` opens its own PostgreSQL connection or probes the SQLite directory -- duplicating what Akka already checks but independently, so the two can report different states.

The health-checks spec and diagnostics docs reference a `data-directory` check that was never implemented. FunkArr uses `DirectoryHealthCheck` instances for directory writability and relies solely on Akka.Hosting for persistence health.

## Goals / Non-Goals

**Goals:**

- Remove the redundant `JournalHealthCheck` -- Akka.Hosting is the single source of truth for persistence health
- Add `DataDirectoryHealthCheck` for SQLite data directory writability (closing spec/docs gap)
- Align the health-checks main spec and docs with reality

**Non-Goals:**

- No changes to `PipelineHealthCheck`, `BudgetHealthCheck`, or Akka.Hosting registrations
- No new option validators (existing coverage is sufficient)
- No MQTT health check (archived)

## Decisions

### 1. DataDirectoryHealthCheck lives in the Host project

The check needs `IOptions<PersistenceOptions>` to resolve the SQLite path. `PersistenceOptions` is moving to `Njord.Core`, so `Njord.Core` could host the check. However, the FunkArr pattern places directory health checks in the project that owns the registration (`FunkArr.Api`), and the check is only relevant when the Host wires up persistence. Placing it in `src/Njord/Health/` keeps it next to where it's registered (`HealthSetupContainer`) and avoids adding health check concerns to Core.

**Alternative**: Put it in `Njord.Core` next to `PersistenceOptions`. Rejected because Core is vocabulary + domain logic, not runtime health probes.

### 2. Conditional registration based on Provider

The `data-directory` check only makes sense for SQLite (file-based). For PostgreSQL, the Akka persistence health check already covers connectivity. `HealthSetupContainer` SHALL check `PersistenceOptions.Provider` and register `DataDirectoryHealthCheck` only when `Provider == PersistenceProvider.Sqlite`.

### 3. Static Evaluate method pattern (FunkArr pattern)

Like the existing `PipelineHealthCheck` and `BudgetHealthCheck`, the new check SHALL have an `internal static Evaluate` method that takes plain values and returns a `HealthCheckResult`. The `CheckHealthAsync` method resolves the directory and calls the static method. Tests call `Evaluate` directly.

### 4. Write probe via DirectoryProbe

`PersistenceOptionsValidator` already does a write probe at startup. The runtime health check uses the same approach: write and delete a `.njord-health-probe` file. If the write or delete fails, the check reports `Unhealthy`. This is a simple, proven pattern from FunkArr's `DirectoryHealthCheck`.

### 5. failureStatus: Unhealthy

The original spec said `Degraded` for data-directory. However, if the SQLite directory becomes unwritable at runtime, Akka persistence will also fail on the next write -- this is not a degraded state, it's a hard failure. Aligning with `Unhealthy` matches the severity and how FunkArr registers its directory checks.

## Risks / Trade-offs

- **[Risk] Write probe adds I/O on every health check poll** -- Mitigation: health checks are polled infrequently (Kubernetes default 10s-30s), and a single file write/delete is negligible. FunkArr does the same.
- **[Risk] Removing JournalHealthCheck changes `/healthz` response shape** -- Mitigation: the check name `journal` disappears, but Akka.Hosting adds its own persistence check names. `/healthz` still returns 200/503 correctly. Monitoring that filters on check name `journal` would need updating, but njord is self-hosted and the docs will reflect the change.

## Open Questions

None.
