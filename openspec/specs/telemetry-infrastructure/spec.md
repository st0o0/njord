# telemetry-infrastructure Specification

## Purpose

Serilog logging infrastructure for the njord service. Wires Serilog as the
ILogger provider (including Akka internal logs) and centralises logging and
health-check setup in the ServiceDefaults project.

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
The Akka.NET actor system SHALL be configured with `Akka.Logger.Serilog` via
`AddLoggerFactory()` so that internal Akka events (dead letters, supervision
failures, persistence errors) are routed through the Serilog pipeline.

#### Scenario: Dead letter is visible in logs
- **WHEN** a message is delivered to a dead letter
- **THEN** a log entry for the dead letter appears in the Serilog console sink

### Requirement: ServiceDefaults project centralises wiring
A `Njord.ServiceDefaults` project SHALL provide extension methods to configure
Serilog and health checks. The main service project and the Aspire AppHost
SHALL reference this project.

#### Scenario: Service uses ServiceDefaults
- **WHEN** the service starts
- **THEN** Serilog and health checks are configured via `Njord.ServiceDefaults`
  extension methods
