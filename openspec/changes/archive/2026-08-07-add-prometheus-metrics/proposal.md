## Why

njord has zero runtime metrics — only health checks and internal budget tracking.
With Alloy + VictoriaMetrics + Grafana already in the stack, adding a Prometheus
`/metrics` endpoint unlocks operational dashboards (fetch health, budget burn,
data quality) without any new infrastructure.

## What Changes

- Add `NjordMetrics` singleton with a `System.Diagnostics.Metrics.Meter` ("Njord"),
  extensible via extension methods per concern area.
- Add `prometheus-net.AspNetCore` for the `/metrics` scrape endpoint and .NET
  runtime metrics (GC, ThreadPool, process stats).
- Instrument the pipeline at five integration points: ingest (fetch outcomes,
  latency), budget (daily/monthly usage, throttle delays), pipeline (poll cycle
  duration, data-change frequency), enrichment (computation time, consensus
  quality, model accuracy), and egress (MQTT dedup ratio, connection state).
- Wire the `/metrics` endpoint in `Program.cs`.

## Non-goals

- OpenTelemetry SDK or OTLP push — Alloy scrapes Prometheus, no need for OTel.
- Grafana dashboard provisioning — dashboards are built manually in Grafana.
- Alerting rules — out of scope for this change.
- Tracing / distributed trace integration — existing Serilog logging is sufficient.
- Metrics for gRPC endpoints — low traffic, not operationally interesting yet.

## Capabilities

### New Capabilities
- `prometheus-metrics`: NjordMetrics singleton, extension method pattern for
  instrument factories, Prometheus scrape endpoint, and integration points
  across ingest/budget/pipeline/enrichment/egress.

### Modified Capabilities
_(none — metrics are additive, no existing behavior changes)_

## Impact

- **New dependency:** `prometheus-net.AspNetCore` in `Njord.csproj`
  (via `Directory.Packages.props`).
- **Modified files:** `Program.cs` (endpoint mapping), `OpenMeteoClient`,
  `BudgetThrottleStage`, `BudgetTrackerActor`, `SchedulerActor`,
  `EnrichmentActor`, `MqttEgressActor`, `MqttConnectionActor`.
- **New files:** `Diagnostics/NjordMetrics.cs` plus 5 extension classes.
- **API budget:** No impact — no additional Open-Meteo requests.
- **Runtime overhead:** Negligible — `System.Diagnostics.Metrics` counters are
  lock-free atomic operations; histogram buckets add ~200 bytes per instrument.
