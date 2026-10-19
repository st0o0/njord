## Why

Logging conventions should match FunkArr: consistent `ILoggingAdapter` in actors (inline init, readonly, `_log`), structured message templates (no string interpolation), correct log levels per event type, and proper category logging. The v2 redesign may have introduced inconsistencies in new code (ProjectionEngine, AQ/Flood alert rules, domain routing).

## What Changes

- Audit all actors for logging pattern: `private readonly ILoggingAdapter _log = Context.GetLogger();`
- Audit all log calls for string interpolation (should use `_log.Info("Fetched {Domain} for {Location}", domain, location)`)
- Audit log levels: Debug for routine operations, Info for lifecycle events, Warning for recoverable errors, Error for unrecoverable
- Verify no logging in PipeTo mappers (per memory: "PipeTo mappers are pure; log in the result handler")
- Verify ActorCategoryLogger is properly configured for new actors
- Fix any violations found

## Non-goals

- Adding new log statements. Just fixing existing ones.
- Changing Serilog configuration.

## Capabilities

### New Capabilities
_(none — convention enforcement)_

### Modified Capabilities
- `logging-conventions`: All code follows FunkArr logging patterns

## Impact

- **All actor-containing projects**: Logging pattern audit and fixes
- **No behavior changes**: Same logic, cleaner logging
