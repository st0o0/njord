## ADDED Requirements

### Requirement: Actors use ILoggingAdapter with inline readonly init
Every actor SHALL declare `private readonly ILoggingAdapter _log = Context.GetLogger();` as an inline field initializer. No `null!` + PreStart pattern.

#### Scenario: Actor logging field
- **WHEN** any actor class is inspected
- **THEN** it SHALL have `private readonly ILoggingAdapter _log = Context.GetLogger();`

### Requirement: Log calls use structured message templates
All log calls SHALL use message templates with named placeholders, not string interpolation. `_log.Info("Fetched {Domain} for {Location}", domain, location)` not `_log.Info($"Fetched {domain} for {location}")`.

#### Scenario: No string interpolation in log calls
- **WHEN** production code is scanned for `_log.*($"` patterns
- **THEN** zero matches SHALL be found

### Requirement: No logging in PipeTo mappers
PipeTo success/failure mapper lambdas SHALL NOT contain log calls. Logging SHALL happen in the message handler that processes the PipeTo result.

#### Scenario: PipeTo mappers are pure
- **WHEN** PipeTo calls are inspected
- **THEN** the success and failure mapper lambdas SHALL contain only mapping logic, no `_log` calls
