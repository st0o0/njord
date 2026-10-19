## Context

FunkArr logging: actors use `ILoggingAdapter` (inline readonly `_log`), services use `ILogger<T>`, all log calls use structured message templates, no string interpolation, no logging in PipeTo mappers.

## Goals / Non-Goals

**Goals:** Consistent logging across all projects per FunkArr conventions.
**Non-Goals:** New log statements, Serilog config changes.

## Decisions

### D1: Audit scope
Every `.cs` file in production `src/Njord.*` projects that contains a log call.

### D2: Fix categories
- Actors: `_log = Context.GetLogger()` → category is the actor's type name via `ActorCategoryLogger`
- Services: `ILogger<T>` → category is the service type name
- Static classes: no logging (pure functions). If logging is needed, it's a design smell.

### D3: Level guidelines
| Level | Use for |
|-------|---------|
| Debug | Routine data flow (forecast received, event mapped) |
| Info | Lifecycle (actor started, connected, pipeline materialized) |
| Warning | Recoverable (fetch failed, retry scheduled, stale data) |
| Error | Unrecoverable (stream terminated, persistence failure) |
