## ADDED Requirements

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

## REMOVED Requirements

### Requirement: prometheus-net metrics adapter
**Reason**: Replaced by native OpenTelemetry SDK. The `Metrics.ConfigureMeterAdapter` bridge, `UseHttpMetrics()`, and `MapMetrics()` from prometheus-net are removed.
**Migration**: Replace prometheus-net packages with OpenTelemetry packages. The `/metrics` endpoint remains available via `MapPrometheusScrapingEndpoint()`.
