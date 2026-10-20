# per-domain-telemetry Specification

## Purpose

Per-domain metrics: each feature library owns its `Meter` and instrument fields in an internal `Telemetry` class, and the host registers every meter explicitly.
## Requirements
### Requirement: Each domain owns its own Meter and instrument fields
Each feature library that emits metrics SHALL contain an `internal static class Telemetry` with an `internal static readonly Meter` named `Njord.<Domain>` and `internal static readonly` instrument fields. Instruments SHALL NOT be created via extension methods that return fresh instances per call. The observable budget gauges SHALL be created once, through `BudgetTelemetry.Observe` on the `Njord.Budget` meter.

#### Scenario: Instrument singleton guarantee
- **WHEN** two classes in the same domain both reference the same metric counter
- **THEN** they reference the same `static readonly` field — not two independently created instrument instances

#### Scenario: Domain meter isolation
- **WHEN** `Njord.OpenMeteo` is inspected
- **THEN** its metrics are defined in `Njord.OpenMeteo/Telemetry.cs` with `Meter("Njord.OpenMeteo")`, not in a shared `Njord.Core` file

### Requirement: Meters are registered explicitly in the host
The host SHALL list every exported meter by name in `CoreSetupContainer.NjordMeters` (`Njord.OpenMeteo`, `Njord.Budget`, `Njord.Polling`, `Njord.Pipeline`), and the prometheus-net meter adapter SHALL export only instruments whose meter name is in that list. `Njord.Core` has no meter; domain metrics stay in their feature library.

#### Scenario: New domain meter is discoverable
- **WHEN** a new feature library adds metrics
- **THEN** it must add its meter name to `NjordMeters` — a missing entry means its metrics are not exported
