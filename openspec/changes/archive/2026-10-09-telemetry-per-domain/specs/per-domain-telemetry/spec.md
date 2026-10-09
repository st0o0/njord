## ADDED Requirements

### Requirement: Each domain owns its own Meter and instrument fields
Each feature library that emits metrics SHALL contain an `internal static class Telemetry`
with a `private static readonly Meter` named `Njord.<Domain>` and `internal static readonly`
instrument fields. Instruments SHALL NOT be created via extension methods that return fresh
instances per call.

#### Scenario: Instrument singleton guarantee
- **WHEN** two classes in the same domain both reference the same metric counter
- **THEN** they reference the same `static readonly` field — not two independently created
  instrument instances

#### Scenario: Domain meter isolation
- **WHEN** `Njord.Ingest` is inspected
- **THEN** its metrics are defined in `Njord.Ingest/Telemetry.cs` with
  `Meter("Njord.Ingest")`, not in a shared `Njord.Core/Diagnostics/` file

### Requirement: Meters are registered explicitly in the host
The host's telemetry setup SHALL register each domain's meter by name
(`.AddMeter("Njord.Ingest", "Njord.Pipeline", ...)`) instead of using a string-predicate
filter.

#### Scenario: New domain meter is discoverable
- **WHEN** a new feature library adds metrics
- **THEN** it must add its meter name to the host's explicit registration list — a missing
  registration means metrics are not exported, caught during integration testing
