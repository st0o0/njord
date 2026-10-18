## Context

Today: `NjordHealthMonitor` (Core) with `Mqtt`, `Poll`, `Budget` slots; writers `PollManager` (on Changed and Unchanged reports), `MqttConnection` (connect and disconnect), `BudgetTracker` (after each persisted `ApiCallRecorded`); `ConfigManager` updates the monthly limit on budget changes; readers `PipelineHealthCheck`, `MqttConnectionHealthCheck` and the budget gauges (`BudgetGaugeRegistration`). An earlier spec states that no health check sends messages to actors, so that `/healthz` does not depend on mailbox responsiveness.

FunkArr reference: `DirectoryHealthCheck`, `FfmpegHealthCheck`, `MediathekViewWebHealthCheck` are sealed `IHealthCheck` classes with an `internal static Probe` seam, registered per feature container with `failureStatus` (`Unhealthy` for directories and ffmpeg, `Degraded` for the optional web service); `/healthz` has a 5-second request timeout and 200 for Healthy and Degraded; `/alive` is unconditional; no health state object exists.

## Goals / Non-Goals

**Goals:** remove the Core monitor, give each feature its own tiny state and check, add checks for the journal, data directory and budget, move thresholds to options, split gauges from health.

**Non-Goals:** actor ask-based checks (they would reverse the mailbox-independence decision), external probes of Open-Meteo or the broker from `/healthz` (a free service must not be hit by every probe, and broker state comes from the connection actor), readiness/liveness endpoint splitting, health UI.

## Decisions

1. **Feature-owned state, no asks.** `PollHealthState` (Pipeline), `MqttConnectionState` (Mqtt), `BudgetUsageState` (Budget) are `internal sealed` classes holding one immutable record behind `Volatile` read/write, injected as concrete types into the owning actor (like the monitor today but local) and into the feature's check. Actors still receive a shared object through the constructor, an accepted pattern here because the object is private to the feature and single-writer.
2. **Freshness rule stays time-based.** A dead poll manager shows up as a stale `LastSuccessfulPollUtc` after `PollStalledFactor` intervals, the same detection as today.
3. **Evaluate seam.** Each check class is a thin shell over `internal static HealthCheckResult Evaluate(…)` taking the state record, `DateTimeOffset now` and the relevant options values, so the specs need neither hosting nor a `HealthCheckContext`; a second spec per check verifies registration and `failureStatus` through the service provider.
4. **Journal check.** SQLite: directory of the resolved database path is writable (create and delete a probe file with `Path.GetRandomFileName()`, like FunkArr's `CanWrite`), through a small `IFileProbe` seam for tests. PostgreSQL: `NpgsqlConnection.OpenAsync` with a 3-second cancellation. Placement in the host (`Njord/Health`) because it needs both `PersistenceOptions` and `StorageOptions`.
5. **Budget check reads limits from `ConfigView` at check time** via `BudgetCalculator.GetEffectiveBudget`, so no component needs to push limits anywhere; usage comes from `BudgetUsageState`, which `BudgetTracker` writes on recovery and after each persisted event (this also fixes the gauges showing zero usage after a restart before the first call).
6. **Gauges.** `BudgetUsageState` also feeds the observable gauges for used values; the limit gauges read the effective budget from `ConfigView`. `BudgetGaugeRegistration` takes both instead of the monitor; metric names and semantics are unchanged.
7. **Options.** `HealthOptions` in `Njord.Core/Configuration` (section `Njord:Health`, registered through `AddNjordOptions<HealthOptions>`, hot), validator enforcing `1 < PollOverdueFactor < PollStalledFactor`, positive durations and `0 < BudgetDegradedBelowPercent < 100`; defaults in `appsettings.json` and `ConfigReloadPolicy` entry; checks read `IOptionsMonitor<HealthOptions>` (hot rule from `options-parity-with-funkarr`). The 5-second `/healthz` timeout stays a constant like in FunkArr.
8. **Spec reconciliation first.** `domain-setup-containers` (`HealthSetupContainer`, `BudgetSetupContainer` requirements), `prometheus-metrics` (budget values), `mqtt-actor-topology` and `mqtt-adapter-boundary` (allow-list) mention the monitor; the first apply task writes their MODIFIED deltas with full requirement text.

## Risks / Trade-offs

- A shared object passed to an actor is a smaller version of today's pattern; accepted for mailbox independence and simplicity, contained by `internal` visibility and single-writer rule.
- The journal probe writes a temporary file on every `/healthz` call; the call rate is low (compose health probes every 30 s) and the file is created in the database directory only.
- Moving the pipeline check into `Njord.Pipeline` adds a health-check package reference there (`Microsoft.Extensions.Diagnostics.HealthChecks`), which the Mqtt project already uses.

## Migration Plan

No operator impact besides a new optional `Njord:Health` section and extra checks in `/healthz` output. Order: spec reconciliation, state types and evaluate seams with specs, switch actors and checks, add new checks, options, gauges, remove the monitor and Core health types, update docs and allow-lists.
