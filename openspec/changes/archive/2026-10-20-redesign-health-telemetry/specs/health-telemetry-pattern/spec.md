## MODIFIED Requirements

### Requirement: Telemetry uses push pattern
Actors SHALL push gauge values to static Telemetry fields on every state change. Observable gauges SHALL read these statics. No BackgroundService, no actor Ask, no polling delay.

#### Scenario: Budget gauge updates immediately on API call
- **WHEN** BudgetTracker records an API call
- **THEN** `Telemetry.SetDailyUsed()` SHALL be called in the same Apply, and the observable gauge SHALL reflect the new value on the next scrape

#### Scenario: No BudgetGaugeRegistration BackgroundService
- **WHEN** the codebase is searched for BudgetGaugeRegistration
- **THEN** zero references SHALL exist

### Requirement: Health checks read atomic state without actor Ask
Health checks SHALL read state from a DI-registered `AtomicStateHolder<T>`, not from actor Ask. The check SHALL be synchronous with no timeout.

#### Scenario: Budget health check is synchronous
- **WHEN** `/healthz` is called
- **THEN** BudgetHealthCheck SHALL read `AtomicStateHolder<BudgetUsage>.Current` without sending an actor message

#### Scenario: Health check works when actor is busy
- **WHEN** BudgetTracker is processing a long operation
- **THEN** the health check SHALL still return instantly using the last-pushed state

### Requirement: One Telemetry class per project
Each project SHALL have at most one `internal static class Telemetry` containing ALL metrics (counters, histograms, and observable gauges). No separate `*Telemetry` classes.

#### Scenario: Budget has one Telemetry file
- **WHEN** Njord.Budget is inspected
- **THEN** exactly one Telemetry.cs SHALL exist, containing the Meter, all counters, and all gauge setters
