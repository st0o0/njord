## Why

`NjordHealthMonitor` is one mutable bag in `Njord.Core`: `PollManager`, `MqttConnection` and `BudgetTracker` write three slots into a shared singleton, and three consumers read it (the pipeline check in the host, the MQTT check, and the Prometheus budget gauges). Core therefore knows MQTT, polling and budget (`MqttHealth` is even on the MQTT boundary allow-list), the budget limits are seeded from options and patched by `ConfigManager`, the gauges and the health state are one object, and the thresholds (2x and 3x poll interval, 2 minutes for MQTT) are literals in the checks. Health coverage is thin: nothing checks that the journal location is writable or reachable, the override-file directory, or budget exhaustion, and neither existing check has a spec.

FunkArr's health checks are small, stateless, feature-owned classes: each check is a sealed `IHealthCheck` with an `internal static Probe…` method as its test seam, registered by the setup container of the feature that owns the dependency with an explicit `failureStatus` (`Unhealthy` for essentials, `Degraded` for optional dependencies); `/healthz` answers 200 for Healthy and Degraded and 503 for Unhealthy with a 5-second request timeout, and `/alive` is unconditional. njord's checks are about freshness and connection state, which need some state, but that state should be owned by the feature, not by Core.

## What Changes

- Remove `NjordHealthMonitor`, `MqttHealth`, `PollHealth`, `BudgetHealth` and `HealthSetupContainer`'s monitor from `Njord.Core`; the MQTT allow-list entry for `MqttHealth` goes away.
- Each feature owns a small internal, thread-safe health state written only by its own actor and read by its own check, registered by that feature's setup container: `PollHealthState` in `Njord.Pipeline` (last successful poll), `MqttConnectionState` in `Njord.Mqtt`, `BudgetUsageState` in `Njord.Budget`. Checks never send actor messages (this keeps the earlier decision that `/healthz` does not depend on mailbox responsiveness).
- Checks follow the FunkArr shape: sealed class, `internal static` probe/evaluate method taking plain values and a `TimeProvider` result, `failureStatus` chosen at registration, one spec per check with `FakeTimeProvider`. The poll check (`poll-freshness`) moves into `Njord.Pipeline`, the MQTT check stays in `Njord.Mqtt`.
- New checks: `journal` (Unhealthy: SQLite file directory writable, or PostgreSQL connection opens within 3 s), `data-directory` (Degraded: the override-file directory is writable, since config writes would fail) and `budget` (Degraded below 5 % remaining, Unhealthy at 0; limits are computed from `ConfigView` at check time, so a hot budget change is visible immediately).
- Thresholds become options in a new `Njord:Health` section (`PollOverdueFactor` 2, `PollStalledFactor` 3, `MqttUnhealthyAfter` 00:02:00, `BudgetDegradedBelowPercent` 5), defaults in `appsettings.json`, hot options read through the monitor.
- The Prometheus budget gauges read a gauge source owned by `Njord.Budget` (written by `BudgetTracker` on every persisted usage change and on recovery), independent of health; metric names are unchanged. `ConfigManager` no longer writes budget limits anywhere; the limit gauges read the effective budget from `ConfigView`.
- `/healthz` gets a 5-second request timeout; `/alive` stays unconditional; no further endpoints are added.

## Capabilities

### New Capabilities
<!-- covered by requirements in health-checks -->

### Modified Capabilities
- `health-checks`: checks are feature-owned and state is feature-owned; new journal, data-directory and budget checks.
- `health-monitor`: the shared monitor is removed; health state records are feature-owned and atomic.

## Impact

- `Njord.Core/Health`, `Njord.Pipeline` (PollManager, new state and check), `Njord.Mqtt` (MqttConnection, check, state), `Njord.Budget` (BudgetTracker, gauge registration, new check), host (`HealthSetupContainer`, journal and data-directory checks, `/healthz` timeout), `ConfigManager`, `Njord.Core/Configuration` (HealthOptions), architecture allow-lists, specs and docs (`docs/` health page, `AGENTS.md`).
- No wire change; `/healthz` and `/alive` keep their status codes and the `/alive` contract used by the e2e plan. Metrics names stay. Not breaking; commit type `refactor:` (new checks as `feat:` in the same change).
- Applies after `feature-owned-options` (Core becomes feature-free) and replaces the health-check items in `test-gaps-and-drift`, whose specs are written against the new checks.
