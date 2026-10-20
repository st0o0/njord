## Why

Budget telemetry uses a BackgroundService that polls an actor every 5 seconds via Ask to fill observable gauges — complex, laggy, and un-FunkArr. FunkArr pushes gauge values directly from actors via `Telemetry.SetX()` statics. Health checks also Ask actors (3s timeout) instead of reading atomic state directly. Two telemetry files exist per feature instead of one.

## What Changes

- **Kill BudgetGaugeRegistration** BackgroundService — replace with push pattern
- BudgetTracker calls `Telemetry.SetDailyUsed(n)` / `Telemetry.SetMonthlyUsed(n)` on state change
- Observable gauges read static fields — no polling, no Ask, instant
- **Merge BudgetTelemetry into Telemetry** — one file per project (FunkArr pattern)
- Budget limits computed once at startup, stored as Telemetry statics
- **Health checks read atomic state directly** — no actor Ask
  - BudgetTracker writes `BudgetUsageState` to a DI-registered holder
  - PollManager writes `PollHealthState` to a holder
  - MqttConnection writes `MqttConnectionStatus` to a holder
  - Health checks read synchronously from holders — pure `Evaluate()` method
- Remove 3-second timeouts from health check paths
- Each health check: `sealed class XHealthCheck : IHealthCheck` with `static Evaluate(state)` (testable without DI)

## Non-goals

- Changing health check thresholds or degraded/unhealthy logic.
- Adding new metrics.

## Capabilities

### New Capabilities
- `health-telemetry-pattern`: FunkArr-aligned push telemetry + synchronous health checks

### Modified Capabilities
- `per-domain-telemetry`: One Telemetry class per project, push pattern
- `health-checks`: Synchronous state reads, no actor Ask

## Impact

- **Njord.Budget**: BudgetGaugeRegistration deleted, Telemetry merged, BudgetTracker pushes
- **Njord.Pipeline**: PollManager pushes PollHealthState to holder
- **Njord.Mqtt**: MqttConnection pushes status to holder
- **Njord (host)**: Health check registration simplified
- **Tests**: Pure Evaluate() tests, no actor setup needed for health checks
