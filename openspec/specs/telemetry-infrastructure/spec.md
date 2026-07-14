# telemetry-infrastructure Specification

## Purpose

Serilog logging and OpenTelemetry tracing/metrics infrastructure for the njord
service. Centralises instrument definitions in `NjordTelemetry`, wires Serilog
as the ILogger provider (including Akka internal logs), and configures opt-in
OTLP export.

## Requirements

### Requirement: Serilog is the logging provider
The service SHALL use Serilog as the `Microsoft.Extensions.Logging` provider
via `Serilog.Extensions.Hosting`. All `ILogger<T>` output SHALL flow through
the Serilog pipeline.

#### Scenario: Application logs flow through Serilog
- **WHEN** an actor logs a message via `ILogger<T>`
- **THEN** the message is processed by Serilog sinks (console and, if
  configured, OpenTelemetry)

### Requirement: Human-readable console output
The Serilog console sink SHALL produce human-readable output using an output
template that includes timestamp, level, and message. The console sink SHALL
always be active regardless of other configuration.

#### Scenario: Console output is readable
- **WHEN** the service writes a log entry
- **THEN** the console output includes a human-readable timestamp, abbreviated
  log level, and the rendered message — not JSON

### Requirement: Serilog enrichers provide context
The Serilog pipeline SHALL enrich log entries with machine name, thread id,
and OpenTelemetry span/trace IDs (when a span is active).

#### Scenario: Log entry includes trace correlation
- **WHEN** a log entry is written inside an active Activity span
- **THEN** the log entry properties include `SpanId` and `TraceId` matching
  the active span

### Requirement: Akka internal logs flow through Serilog
The Akka.NET actor system SHALL be configured with `Akka.Logger.Serilog` via
`AddLoggerFactory()` so that internal Akka events (dead letters, supervision
failures, persistence errors) are routed through the Serilog pipeline.

#### Scenario: Dead letter is visible in logs
- **WHEN** a message is delivered to a dead letter
- **THEN** a log entry for the dead letter appears in the Serilog console sink

### Requirement: OpenTelemetry tracing is registered
The service SHALL register an `ActivitySource` named `"Njord"` with the OTel
SDK. The OTel tracing pipeline SHALL also include `HttpClient` and
`AspNetCore` auto-instrumentation.

#### Scenario: Custom activity source is registered
- **WHEN** the OTel SDK is initialised
- **THEN** the `"Njord"` activity source is subscribed and spans from it are
  exported

### Requirement: OpenTelemetry metrics are registered
The service SHALL register a `Meter` named `"Njord"` with the OTel SDK.

#### Scenario: Custom meter is registered
- **WHEN** the OTel SDK is initialised
- **THEN** instruments created from the `"Njord"` meter are collected and
  exported

### Requirement: OTLP export is opt-in
The OTel SDK SHALL export traces, metrics, and logs via OTLP only when the
`OTEL_EXPORTER_OTLP_ENDPOINT` environment variable is set. When not set, no
OTLP export SHALL occur and logs SHALL go to console only.

#### Scenario: No collector configured
- **WHEN** `OTEL_EXPORTER_OTLP_ENDPOINT` is not set
- **THEN** no OTLP exporter is active and logs appear only on the console

#### Scenario: Collector configured
- **WHEN** `OTEL_EXPORTER_OTLP_ENDPOINT` is set to a valid endpoint
- **THEN** traces, metrics, and logs are exported via OTLP to that endpoint

### Requirement: NjordTelemetry is the single source of instrument definitions
All `ActivitySource`, `Meter`, `Counter`, `Histogram`, and `UpDownCounter`
instances SHALL be defined as static fields in a single `NjordTelemetry`
class. No other class SHALL create OTel instruments directly.

#### Scenario: Instrument names are centralised
- **WHEN** a developer needs to record a metric
- **THEN** they reference a static field on `NjordTelemetry` (e.g.
  `NjordTelemetry.FetchTotal`) — they do not create their own `Counter`

### Requirement: Resource attributes identify the service
The OTel SDK SHALL set resource attributes `service.name` to `"njord"` and
`service.version` to the assembly informational version.

#### Scenario: Service resource is set
- **WHEN** a trace or metric is exported
- **THEN** the resource includes `service.name=njord` and `service.version`
  matching the running assembly version

### Requirement: ServiceDefaults project centralises wiring
A `Njord.ServiceDefaults` project SHALL provide extension methods to configure
Serilog, OTel tracing, OTel metrics, OTLP export, and health checks. The
main service project and the Aspire AppHost SHALL reference this project.

#### Scenario: Service uses ServiceDefaults
- **WHEN** the service starts
- **THEN** Serilog, OTel tracing, OTel metrics, and health checks are
  configured via `Njord.ServiceDefaults` extension methods
