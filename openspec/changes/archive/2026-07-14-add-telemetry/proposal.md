## Why

njord currently has zero observability infrastructure: no structured logging
provider, no distributed tracing, no application metrics, and no meaningful
health checks. The `/healthz` endpoint always returns `Healthy` regardless of
actual service state. When something goes wrong — a model stops returning data,
MQTT disconnects, or the pipeline stalls — the only signal is silence on the HA
side.

Adding telemetry now (before the service goes live) means every future change
ships observable by default, and operators can connect their own monitoring
stack without touching njord code.

## What Changes

- **Serilog as MEL provider** — human-readable console sink (always on),
  OpenTelemetry sink (opt-in via `OTEL_EXPORTER_OTLP_ENDPOINT`), enrichers
  for machine name, thread id, and trace/span correlation.
- **Akka.Logger.Serilog bridge** — Dead Letters, supervision failures, and
  persistence errors flow through the same Serilog pipeline as application logs.
- **OpenTelemetry tracing** — `ActivitySource("Njord")` with spans for HTTP
  fetch (`njord.fetch`) and MQTT publish (`njord.mqtt.publish`), tagged with
  location, model, and status.
- **OpenTelemetry metrics** — `Meter("Njord")` with counters (polls, fetches,
  failures, MQTT publishes, discovery publishes, data changes, reconnects),
  histograms (fetch duration, MQTT publish duration, poll cycle duration), and
  an UpDownCounter for MQTT connection state.
- **ServiceDefaults project** — `Njord.ServiceDefaults` centralises OTel SDK
  wiring, Serilog configuration, and health check registration. Aspire
  Dashboard receives all signals in dev; OTLP export is opt-in in prod.
- **Real health checks** — `MqttConnectionHealthCheck` (degraded/unhealthy on
  disconnect duration) and `PipelineHealthCheck` (healthy/degraded/unhealthy
  based on time since last successful poll vs configured interval). Shared
  state via DI, not actor Ask. Separate `/alive` (liveness) endpoint.
- **Static `NjordTelemetry` class** — single source of truth for
  `ActivitySource`, `Meter`, and all instrument instances. No magic strings.

## Non-goals

- Replacing `ILogger<T>` in actors — Serilog plugs in behind MEL; existing log
  call sites stay unchanged.
- Serilog file sink — container logs go to stdout; persistence is the
  collector's job.
- Forcing a specific collector or backend — njord exports OTLP; the operator
  decides where it goes (Grafana, Seq, Jaeger, nothing).
- Custom Akka HOCON log config — the bridge uses `AddLoggerFactory()` which
  routes through MEL/Serilog; no HOCON `akka.loggers` section needed.
- API-budget impact: this change adds zero additional HTTP requests — it
  instruments existing calls only.

## Capabilities

### New Capabilities

- `telemetry-infrastructure`: Serilog wiring, OTel SDK setup, ServiceDefaults
  project, `NjordTelemetry` static class, OTLP opt-in export.
- `pipeline-instrumentation`: Traces and metrics at fetch, poll, MQTT publish,
  discovery, and connection lifecycle points.
- `health-checks`: Real `MqttConnectionHealthCheck` and `PipelineHealthCheck`
  with shared state, `/alive` liveness endpoint.

### Modified Capabilities

- `health-endpoint`: Currently always-healthy; will gain real checks and a
  separate `/alive` liveness endpoint.

## Impact

- **New project**: `Njord.ServiceDefaults` (referenced by `Njord` and
  `Njord.AppHost`).
- **New packages**: Serilog, Serilog.Extensions.Hosting,
  Serilog.Sinks.Console, Serilog.Sinks.OpenTelemetry, Akka.Logger.Serilog,
  OpenTelemetry.Extensions.Hosting, OpenTelemetry.Exporter.OpenTelemetryProtocol,
  OpenTelemetry.Instrumentation.AspNetCore, OpenTelemetry.Instrumentation.Http.
- **Modified files**: `Program.cs` (Serilog bootstrap), `NjordServiceSetup.cs`
  (ServiceDefaults reference, health check registration),
  `NjordActorSystemSetup.cs` (Akka logger bridge),
  `NjordApplicationSetup.cs` (health endpoint mapping), `Njord.AppHost/Program.cs`
  (ServiceDefaults reference).
- **Instrumented actors**: `PipelineActor`, `SchedulerActor`,
  `MqttConnectionActor`, `DiscoveryActor`.
- **New DI service**: `NjordHealthState` (shared mutable state read by health
  checks, written by actors).
