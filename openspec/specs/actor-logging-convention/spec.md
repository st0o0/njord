## Purpose

Defines how actors declare their logger: a single inline-initialized, readonly `ILoggingAdapter _log` field instead of PreStart assignment or inline `Context.GetLogger()` calls.

## Requirements

### Requirement: Actor logging uses inline readonly field initialization

Every actor SHALL declare its logger as
`private readonly ILoggingAdapter _log = Context.GetLogger();` —
inline initialized, readonly, named `_log`.

#### Scenario: Actor with PreStart logging init
- **WHEN** an actor has `private ILoggingAdapter _log = null!;` and assigns `_log = Context.GetLogger()` in PreStart
- **THEN** the field SHALL be changed to `private readonly ILoggingAdapter _log = Context.GetLogger();` and the PreStart assignment removed

#### Scenario: Actor with inline Context.GetLogger() calls
- **WHEN** an actor calls `Context.GetLogger()` inline in command handlers (e.g. BudgetTracker)
- **THEN** a `private readonly ILoggingAdapter _log = Context.GetLogger();` field SHALL be added and handlers SHALL use `_log`

#### Scenario: PreStart with other logic
- **WHEN** an actor's PreStart contains logic beyond `_log = Context.GetLogger()`
- **THEN** the PreStart method SHALL be kept with only the `_log` assignment removed
