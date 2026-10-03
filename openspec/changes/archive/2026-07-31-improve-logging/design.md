## Context

Njord uses Serilog via `ILogger<T>` (Microsoft DI) in most actors and services, with Akka's `Context.GetLogger()` in three persistence actors. The Akka logger is configured via `AddLoggerFactory()` which routes through the same Serilog pipeline. The console output template is `[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}` — no source attribution.

Current pain points:
- **40+ framework log lines per poll cycle** from `System.Net.Http.HttpClient` (4 lines × 10 models).
- **No way to identify which actor logged a message** — multiple actors emit identical "SourceRef received" / "SinkRef received" strings.
- **Plumbing logs at Information level** drown out operationally relevant events.
- **Enrichment and MQTT egress are silent** — no log when enrichment computes or state messages are published.
- **Debug logs lack context** — "Pipeline SinkRef received" tells you nothing about what happened.

## Goals / Non-Goals

**Goals:**
- Reduce production log noise by ~70% (from ~60 lines to ~15 per poll cycle)
- Every log line attributable to a subsystem via `[{Subsystem}]` tag
- Operationally relevant events visible at Information level
- Debug level useful for troubleshooting with concrete data (intervals, counts, hashes)

**Non-Goals:**
- Switching between `ILogger<T>` and `Context.GetLogger()` — both stay as-is
- Structured sinks, correlation IDs, distributed tracing
- Trace-level or payload-dumping logs

## Decisions

### 1. Explicit Subsystem property via `LogContext.PushProperty`

Each actor/service pushes a `Subsystem` property in its constructor using Serilog's `LogContext.PushProperty`. This is a `IDisposable` that lives for the object lifetime.

**Why not Akka's `WithContext`**: Only works with `ILoggingAdapter` from `Context.GetLogger()`. 11 of 14 logging classes use `ILogger<T>`. Switching all to Akka's logger would lose DI testability for no gain.

**Why not a namespace-based enricher**: The user explicitly wants per-actor control over the tag. A namespace convention (`Njord.Pipeline.*` → `"pipeline"`) would be implicit and fragile when classes move.

**Why not `ILogger.BeginScope`**: Creates a new `IDisposable` scope per call. `LogContext.PushProperty` in the constructor is one allocation for the actor's entire lifetime.

Subsystem values (fixed set):
| Subsystem   | Classes |
|-------------|---------|
| `pipeline`  | `PipelineActor`, `SchedulerActor`, `BudgetTrackerActor` |
| `egress`    | `ModelStateActor`, `EgressActor` |
| `mqtt`      | `MqttConnectionActor`, `MqttEgressActor`, `DiscoveryActor`, `MqttNetPublisher` |
| `enrich`    | `EnrichmentActor`, `HistoryEnrichment`, `ForecastHistoryActor` |
| `grpc`      | `GrpcSnapshotConsumerActor`, `OpsGrpcService`, `AdminGrpcService`, `ForecastSnapshotActor`, `EnrichmentSnapshotActor` |
| `stream`    | `StreamSupervision` (static — uses passed logger, property comes from caller) |

Implementation: Add a private field `private readonly IDisposable _logScope;` and set it in the constructor:
```csharp
_logScope = LogContext.PushProperty("Subsystem", "pipeline");
```

For actors: dispose in `PostStop`. For services: implement `IDisposable` or rely on DI container lifetime (gRPC services are scoped — the property auto-pops when the scope ends).

**Important**: `LogContext` is `AsyncLocal`-based. In actors this is fine because actor message processing is single-threaded. For stream lambdas that run on the stream dispatcher, the property is inherited from the materializing actor's async-local context — this works correctly because `MaterializeGraph` runs inside the actor's message processing.

### 2. Output template

```
[{Timestamp:HH:mm:ss} {Level:u3}] [{Subsystem,-8}] {Message:lj}{NewLine}{Exception}
```

The `,-8` left-aligns and pads to 8 chars for visual alignment. Framework logs without a `Subsystem` property will show `[]` — which is fine, they're filtered to Warning anyway and rare.

### 3. Framework log level filtering

In `appsettings.json`:
```json
{
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Akka": "Warning",
        "Microsoft.AspNetCore": "Warning",
        "Microsoft.AspNetCore.Hosting.Diagnostics": "Information",
        "System.Net.Http.HttpClient": "Warning",
        "Grpc.AspNetCore.Server": "Warning"
      }
    }
  }
}
```

**Why Serilog section instead of Logging section**: The app uses `ReadFrom.Configuration` — Serilog's own config section gives finer control and is the canonical way. The existing `Logging` section can be removed to avoid confusion.

**Why keep `Microsoft.AspNetCore.Hosting.Diagnostics` at Info**: This is the source for "Application started", "Now listening on" — useful startup confirmation.

In `appsettings.Development.json`, set `Default` to `Debug` so all njord Debug logs are visible during development.

### 4. Level reassignment and new logs

See the spec for the complete matrix. Design principle:

- **Error**: Something that should never happen and indicates a bug or infrastructure failure.
- **Warning**: Expected failure paths that have automatic recovery (retries, reconnects, timeouts).
- **Information**: State transitions and results an operator cares about. "What happened this cycle?"
- **Debug**: Internal mechanics an operator only needs when troubleshooting. "How did it happen?"

Key new logs and their content:

| Level | Actor | Message | Properties |
|-------|-------|---------|------------|
| INF | `SchedulerActor` | `"Poll complete for {Location}"` | `{Changed}`, `{Total}`, `{Duration}` |
| INF | `MqttConnectionActor` | `"MQTT connected to {Host}:{Port}"` | host, port |
| INF | `EnrichmentActor` | `"Enrichment computed for {Location}"` | `{Features}` (comma-joined list) |
| DBG | `SchedulerActor` | `"Scheduling next poll for {Location}/{Model} at {NextPoll}"` | location, model, absolute time |
| DBG | `SchedulerActor` | `"Hash unchanged for {Location}/{Model}"` | location, model |
| DBG | `MqttEgressActor` | `"Published {Count} state messages for {Location}"` | count, location |
| DBG | `ModelStateActor` | `"Received {RefType} from {Source}"` | ref type name, source actor path |
| DBG | `EnrichmentActor` | `"Received {RefType} from {Source}"` | ref type name, source actor path |

For demoted logs (INF→DBG), the message text stays the same but gains context:
- `"Pipeline SinkRef received"` → `"SinkRef received from {Source}"` at Debug
- `"Pipeline SourceRef received"` → `"SourceRef received from {Source}"` at Debug

## Risks / Trade-offs

- **[`LogContext` and thread pools]** → `LogContext` uses `AsyncLocal`, which flows correctly through `async/await` and actor message processing. Stream stages that run on the Akka stream dispatcher inherit the async-local from the materializing context. If a future change introduces `Task.Run` or `ThreadPool.QueueUserWorkItem` inside an actor, the property would not flow. Mitigation: this is the existing pattern for all Serilog enrichment — not specific to this change.
- **[Missing Subsystem on framework logs]** → Framework logs (the few that pass the level filter) will show `[]` as subsystem. This is acceptable — they're rare at Warning+ level and identifiable by their SourceContext.
- **[`PostStop` disposal in actors]** → If an actor crashes before `PostStop`, the `IDisposable` from `PushProperty` leaks. In practice, `AsyncLocal` values are GC'd with the execution context — there's no resource leak, just a dangling `IDisposable`. No mitigation needed.

## Open Questions

_(none)_
