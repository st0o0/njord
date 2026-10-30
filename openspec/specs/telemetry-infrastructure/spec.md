# telemetry-infrastructure Specification

## Purpose

Serilog logging infrastructure and OpenTelemetry metrics for the njord service: Serilog is the `ILogger` provider, writes human-readable console output with context enrichers, and receives the Akka internal logs. OpenTelemetry collects .NET Meter instruments and exposes them via Prometheus.

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
  log level, and the rendered message -- not JSON

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

### Requirement: Metrics are collected via OpenTelemetry SDK
The service SHALL use the OpenTelemetry .NET SDK (`AddOpenTelemetry().WithMetrics()`) to collect metrics from .NET `Meter` instruments. Each Njord meter SHALL be registered via `AddMeter()`. The service SHALL expose metrics via the OpenTelemetry Prometheus exporter (`AddPrometheusExporter()`).

#### Scenario: Njord meters are collected
- **WHEN** a Njord domain library records a metric (e.g. `Telemetry.HttpRequests.Add(1)`)
- **THEN** the metric is collected by the OpenTelemetry SDK and exposed at the Prometheus scraping endpoint

#### Scenario: Standard .NET instrumentation is active
- **WHEN** the service is running
- **THEN** ASP.NET Core, process, and runtime metrics are collected via OpenTelemetry instrumentation packages

#### Scenario: Prometheus scraping endpoint is available
- **WHEN** a Prometheus scraper requests the metrics endpoint
- **THEN** the response contains all registered Njord meters plus standard instrumentation metrics in Prometheus exposition format

### Requirement: Service resource is identified
The OpenTelemetry resource SHALL identify the service as `Njord` with the assembly version, using `ConfigureResource(r => r.AddService(...))`.

#### Scenario: Service name in metrics
- **WHEN** metrics are exported
- **THEN** the resource identifies the service as `Njord` with the current assembly version
