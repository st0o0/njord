# telemetry-infrastructure Specification

## Purpose

Serilog logging infrastructure for the njord service: Serilog is the `ILogger` provider, writes human-readable console output with context enrichers, and receives the Akka internal logs.

## Requirements
### Requirement: Serilog is the logging provider
The service SHALL use Serilog as the `Microsoft.Extensions.Logging` provider
via `Serilog.Extensions.Hosting`. All `ILogger<T>` output SHALL flow through
the Serilog pipeline.

#### Scenario: Application logs flow through Serilog
- **WHEN** an actor logs a message via `ILogger<T>`
- **THEN** the message is processed by the Serilog console sink

### Requirement: Human-readable console output
The Serilog console sink SHALL produce human-readable output using an output
template that includes timestamp, level, and message. The console sink SHALL
always be active regardless of other configuration.

#### Scenario: Console output is readable
- **WHEN** the service writes a log entry
- **THEN** the console output includes a human-readable timestamp, abbreviated
  log level, and the rendered message — not JSON

### Requirement: Serilog enrichers provide context
The Serilog pipeline SHALL enrich log entries with machine name and thread id.

#### Scenario: Log entry includes machine context
- **WHEN** a log entry is written
- **THEN** the log entry properties include `MachineName` and `ThreadId`

### Requirement: Akka internal logs flow through Serilog
The Akka.NET actor system SHALL be configured with `ClearLoggers()` and `AddLoggerFactory()` (Akka.Hosting) so that actor logs and internal Akka events (dead letters, supervision failures, persistence errors) are routed through `Microsoft.Extensions.Logging` into the Serilog pipeline.

#### Scenario: Dead letter is visible in logs
- **WHEN** a message is delivered to a dead letter
- **THEN** a log entry for the dead letter appears in the Serilog console sink

