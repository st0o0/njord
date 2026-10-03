## Context

njord has zero observability infrastructure. All logging uses the default
`Microsoft.Extensions.Logging` console provider with a single
`"Default": "Information"` config line. Actors consistently use `ILogger<T>`
via DI with structured message templates — this is the right foundation and
stays unchanged.

There are no traces, no metrics, no custom health checks. The `/healthz`
endpoint always returns `Healthy`. Akka internal events (dead letters,
supervision, persistence) are invisible because no Akka logger adapter is
configured.

The Aspire AppHost exists but has no ServiceDefaults project.

## Goals / Non-Goals

**Goals:**

- All three OTel signals (logs, traces, metrics) emitted by the service.
- Serilog as the logging provider with human-readable console output and
  optional OTLP export.
- Akka internal events visible in the same log pipeline.
- Meaningful health checks that detect real failure states (MQTT disconnect,
  stalled pipeline).
- Operator connects their own collector via `OTEL_EXPORTER_OTLP_ENDPOINT` —
  njord does not prescribe a backend.
- Aspire Dashboard shows all signals in dev mode automatically.

**Non-Goals:**

- Changing any existing `ILogger<T>` call site in actors.
- Adding a Serilog file sink (container stdout is the log stream).
- Shipping a collector, Grafana stack, or any monitoring backend.
- HOCON-based Akka log configuration.
- Instrumenting enrichment sub-computations (consensus, alerts, etc.) — those
  are pure functions; the enclosing fetch→egress spans provide sufficient
  context.

## Decisions

### Decision 1: Serilog as MEL provider (not OTel directly as MEL provider)

Serilog sits between `ILogger<T>` and the export layer. The OTel SDK handles
traces and metrics directly, but logs flow through Serilog.

**Why Serilog over direct OTel log provider:**
- Human-readable console output with Serilog's output templates — OTel's log
  provider produces structured JSON only, which is unreadable without tooling.
- Enrichers (machine name, thread id, span correlation) are declarative and
  composable.
- The Serilog.Sinks.OpenTelemetry sink forwards logs into OTLP when configured,
  so logs still reach the collector alongside traces and metrics.
- Mature ecosystem if future sinks are needed (Seq, Elastic, etc.).

**Alternative considered:** OTel as the sole MEL provider. Rejected because it
cannot produce human-readable console output — the user explicitly requires
readable logs in prod.

### Decision 2: Akka.Logger.Serilog via AddLoggerFactory()

```csharp
builder.WithLoggers(setup => setup.ClearLoggers().AddLoggerFactory());
```

This routes Akka's internal log bus through `ILoggerFactory` → Serilog.
Dead letters, supervision failures, persistence errors, and lifecycle events
all appear in the same pipeline.

**Why not skip it:** Akka internals are invisible without the bridge. Even
though the user doesn't actively want to monitor them, when something goes
wrong in the actor system (supervision storms, persistence failures), those
logs are essential for diagnosis and would otherwise be silently lost.

### Decision 3: Static NjordTelemetry class for all instruments

A single static class holds `ActivitySource("Njord")`, `Meter("Njord")`, and
all `Counter` / `Histogram` / `UpDownCounter` instances. Actors reference
`NjordTelemetry.FetchTotal.Add(...)` etc.

**Why static, not DI:** `ActivitySource` and `Meter` are designed to be
long-lived singletons. The OTel SDK finds them by name at registration time.
DI registration adds ceremony without benefit — the instances are stateless
and thread-safe. This matches the OTel .NET guidance.

**Why one class, not per-actor:** All instrument names and tag keys in one
place make the metric surface discoverable and prevent naming drift.

### Decision 4: Span/metric tags for location and model (not resource attributes)

Location and model are **per-operation tags**, not resource-level attributes.
A single njord instance serves multiple locations and models — resource
attributes are fixed per service instance and cannot vary per span.

Resource attributes: `service.name=njord`, `service.version=<assembly>`.

Span/metric tags: `location`, `model`, `status`, `reason`, `type` — set per
operation.

### Decision 5: Shared NjordHealthState for health checks (not actor Ask)

Health checks read from a `NjordHealthState` object registered as a DI
singleton. Actors write to it via `Interlocked` operations.

**Why not Ask the actor:** The health check's purpose is to detect when an
actor is stuck. An `Ask<HealthStatus>` to a stuck actor deadlocks the health
check — exactly the failure mode it should detect. Shared state with atomic
writes avoids this circular dependency.

```
NjordHealthState (singleton)
  ├─ LastSuccessfulPollUtc     (written by SchedulerActor on HashResult)
  ├─ MqttConnectedSince        (written by MqttConnectionActor)
  ├─ MqttDisconnectedSince     (written by MqttConnectionActor)
  └─ IsMqttConnected            (written by MqttConnectionActor)
```

Health checks read these fields and compare against `TimeProvider.GetUtcNow()`
and the configured poll interval.

### Decision 6: ServiceDefaults project for central wiring

A `Njord.ServiceDefaults` class library project provides extension methods:

- `AddNjordTelemetry(this IHostApplicationBuilder)` — Serilog bootstrap, OTel
  tracing (AddSource "Njord", AddHttpClientInstrumentation,
  AddAspNetCoreInstrumentation), OTel metrics (AddMeter "Njord"), OTLP
  exporter (conditional on endpoint env var).
- `AddNjordHealthChecks(this IHostApplicationBuilder)` — registers
  `MqttConnectionHealthCheck` and `PipelineHealthCheck`.
- `MapDefaultEndpoints(this WebApplication)` — maps `/healthz` (all checks)
  and `/alive` (liveness, always 200).

The AppHost references ServiceDefaults so the Aspire Dashboard automatically
receives all signals in dev mode. In prod (standalone Docker), the same code
runs — OTLP just has no endpoint configured unless the operator sets one.

### Decision 7: Console output format

Serilog console sink with a human-readable output template:

```
[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}
```

No JSON on console. Operators who want structured logs connect a collector
and consume OTLP.

## Risks / Trade-offs

- **[Performance] Histogram allocations in hot path** → The fetch and MQTT
  publish histograms are called per-operation. OTel SDK instruments are
  designed for this; the overhead is a few nanoseconds per `Record()` call.
  No mitigation needed.

- **[Coupling] NjordHealthState shared between actors and health checks** →
  All fields use `Interlocked` / `volatile` for thread safety. The state
  object is a simple data holder with no logic, minimising coupling surface.

- **[Dependency count] ~9 new NuGet packages** → All are mainstream
  (Serilog, OTel SDK). The alternative (fewer packages, less capability) was
  rejected in exploration.

- **[Aspire version coupling] ServiceDefaults ties to Aspire SDK version** →
  ServiceDefaults is a local project, not the Aspire starter template. It
  uses only `OpenTelemetry.Extensions.Hosting` — no tight Aspire SDK coupling
  beyond what the AppHost already has.
