## Context

njord runs as a Docker container with an ASP.NET host (Kestrel on port 8080
for health checks, port 8081 for gRPC). The observability stack is
Alloy → VictoriaMetrics → Grafana, using Prometheus scraping.

Currently there is zero metrics instrumentation. Operational insight comes
from Serilog structured logs and the `/healthz` endpoint. Budget tracking
exists internally in `BudgetTrackerActor` but is not exposed. Data quality
signals (consensus model count, MAE, spread) are computed in the enrichment
pipeline and published to MQTT but not observable from an ops perspective.

## Goals / Non-Goals

**Goals:**
- Expose a `/metrics` Prometheus endpoint on the existing Kestrel host.
- Instrument five concern areas: ingest, budget, pipeline, enrichment, egress.
- Keep metrics decoupled from business logic via a static singleton + extension
  method pattern.
- Include .NET runtime metrics (GC, ThreadPool, process) for free.

**Non-Goals:**
- No OTel SDK, no OTLP push — Alloy scrapes.
- No Grafana dashboard provisioning or alerting rules.
- No tracing spans — Serilog covers that need.
- No gRPC endpoint metrics.

## Decisions

### 1. NjordMetrics as static singleton

`NjordMetrics.Instance` owns a single `System.Diagnostics.Metrics.Meter("Njord")`.
Instruments are created via extension methods on `NjordMetrics`, grouped by
concern area. Consumers store instruments in `static readonly` fields.

```
NjordMetrics.Instance ──┬── .AddFetchTotal()          → Counter<long>
                        ├── .AddFetchDuration()        → Histogram<double>
                        ├── .AddBudgetUsedDaily(fn)    → ObservableGauge<long>
                        ├── .AddPollCycleDuration()    → Histogram<double>
                        ├── .AddEnrichmentDuration()   → Histogram<double>
                        └── .AddMqttDedup()            → Counter<long>
```

**Why not DI?** Metrics instruments are stateless atomic counters — no
lifecycle management, no testability need. Static access eliminates constructor
noise in actors that already have complex DI signatures. The extension method
pattern keeps `NjordMetrics` itself closed for modification.

**Why not ServusMetrics?** The `ServusMetrics` constructor is internal. Rather
than coupling to a Servus release cycle, njord owns its Meter directly. Same
pattern, no dependency. `CounterExtensions` (.Up()/.Down()) from Servus.Core
can still be used if desired.

**Alternative considered:** Per-zone DI-registered metrics classes
(IngestMetrics, EgressMetrics). Rejected — adds 5 constructor parameters
across the codebase for no testability benefit.

### 2. prometheus-net for the scrape endpoint

`prometheus-net.AspNetCore` bridges `System.Diagnostics.Metrics` to the
Prometheus exposition format and maps the `/metrics` endpoint. Also exports
.NET runtime metrics (GC, thread pool, process) automatically.

**Why not OpenTelemetry.Exporter.Prometheus.AspNetCore?** More dependencies
(full OTel SDK), more boilerplate, no benefit when the only consumer is
Prometheus scraping. prometheus-net is lighter and well-maintained.

### 3. Extension method file layout

One extension class per concern area, matching the three-zone architecture
plus pipeline and budget as cross-cutting:

```
Njord/Diagnostics/
├── NjordMetrics.cs                     Meter owner
├── IngestMetricsExtensions.cs          fetch outcomes, latency
├── BudgetMetricsExtensions.cs          budget gauges, throttle
├── PipelineMetricsExtensions.cs        poll cycle, data changes
├── EnrichmentMetricsExtensions.cs      enrichment duration, quality
└── EgressMetricsExtensions.cs          MQTT dedup, connection
```

### 4. Integration points

Each existing component gets minimal metrics calls — no structural changes:

| Component | Metrics | How |
|-----------|---------|-----|
| `OpenMeteoClient` | fetch total, fetch duration | After `FetchAsync` completes — record outcome + elapsed |
| `BudgetThrottleStage` | throttle wait duration | On timer-based resume — record park duration |
| `BudgetTrackerActor` | budget used daily/monthly, limits | ObservableGauge callbacks reading actor state via `NjordHealthState` |
| `SchedulerActor` | poll cycle duration, data changed | On cycle-complete log (already tracks duration) |
| `EnrichmentActor` | enrichment duration | Stopwatch around enrichment computation |
| `EnrichmentActor` | consensus models, spread | After consensus result — set gauges |
| `ForecastHistoryActor` | MAE per model, model weights | After history computation — set gauges |
| `MqttEgressActor` | dedup published/skipped | At existing dedup decision point |
| `MqttConnectionActor` | mqtt connected gauge | On connect/disconnect (already updates HealthState) |

### 5. Budget gauges via NjordHealthState

`BudgetTrackerActor` is a persistent actor — its state isn't directly
accessible. `NjordHealthState` is already a thread-safe DI singleton that
actors write to. Extend it with `BudgetUsedDaily`, `BudgetUsedMonthly`,
`BudgetLimitDaily`, `BudgetLimitMonthly` properties. The actor updates them
on each `BudgetAcquired` event. ObservableGauges read from HealthState.

### 6. Metric naming convention

Follow Prometheus naming conventions: `njord_` prefix, snake_case,
unit suffix where applicable.

| Instrument | Name | Unit | Type |
|-----------|------|------|------|
| Fetch outcomes | `njord_fetch_total` | `{request}` | Counter |
| Fetch latency | `njord_fetch_duration_seconds` | `s` | Histogram |
| Budget daily | `njord_budget_used_daily` | `{request}` | ObservableGauge |
| Budget monthly | `njord_budget_used_monthly` | `{request}` | ObservableGauge |
| Budget limit daily | `njord_budget_limit_daily` | `{request}` | ObservableGauge |
| Budget limit monthly | `njord_budget_limit_monthly` | `{request}` | ObservableGauge |
| Throttle wait | `njord_throttle_wait_seconds` | `s` | Histogram |
| Poll cycle duration | `njord_poll_cycle_duration_seconds` | `s` | Histogram |
| Poll cycle models | `njord_poll_cycle_models` | `{model}` | Gauge |
| Data changed | `njord_data_changed_total` | `{change}` | Counter |
| Enrichment duration | `njord_enrichment_duration_seconds` | `s` | Histogram |
| Consensus models | `njord_consensus_models` | `{model}` | Gauge |
| Consensus spread | `njord_consensus_spread_celsius` | `Cel` | Gauge |
| History MAE | `njord_history_mae_celsius` | `Cel` | Gauge |
| History model weight | `njord_history_model_weight` | `1` | Gauge |
| MQTT dedup | `njord_mqtt_dedup_total` | `{message}` | Counter |
| MQTT connected | `njord_mqtt_connected` | `1` | Gauge |

**Labels** (low cardinality):
- `location` — location slug (2-3 values)
- `model` — Open-Meteo model id (up to 8)
- `outcome` — fetch result reason (4 values)
- `feature` — enrichment type name (6 values)
- `decision` — dedup outcome: `published` / `skipped`

Max cardinality per instrument: ~16 (location × model). Safe for VictoriaMetrics.

## Risks / Trade-offs

**[BudgetThrottleStage has no DI access]** → The stage is constructed in
`PipelineActor` which does have DI. Pass the `Histogram<double>` instance
through the stage constructor. Since we use the static singleton pattern,
this becomes trivial: the stage creates its own instrument from
`NjordMetrics.Instance`.

**[Enrichment gauges are set per-cycle, not continuously]** → Gauges for
consensus models, spread, and MAE only update once per poll cycle (default
60 min). This is fine — the values don't change between cycles, and
Prometheus scraping at 15-30s intervals will see stable values.

**[prometheus-net adds ~500 KB to the container image]** → Negligible for a
Docker container already pulling the .NET runtime.

**[ObservableGauge callbacks must be thread-safe]** → `NjordHealthState` is
already designed for concurrent actor writes + health check reads. Budget
properties follow the same pattern (volatile/Interlocked).
