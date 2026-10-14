# structured-logging-hygiene Specification

## Purpose

Logging conventions: all log templates use named placeholders, and routine operations log at Debug level.

## Requirements
### Requirement: All log templates use named placeholders
Log message templates SHALL use named placeholders (`{Location}`, `{ModelId}`) instead of
positional placeholders (`{0}`, `{1}`) for structured-logging compatibility.

#### Scenario: Structured logging field extraction
- **WHEN** a log message from any actor or service is ingested by a structured logging
  system (Seq, Loki)
- **THEN** all placeholder values are queryable by their named field, not by position index

### Requirement: Routine operations use Debug level
Log calls for operations that occur every poll cycle or every discovery cycle SHALL use
Debug level. Info level is reserved for lifecycle events (actor started/stopped, config
reloaded, MQTT connected/disconnected) and genuinely noteworthy state changes.

#### Scenario: Production log volume at Info level
- **WHEN** njord runs at the default 60-minute poll interval with Info-level minimum
- **THEN** no Info-level message is emitted per poll cycle — only on startup, shutdown,
  config change, or connection state change

