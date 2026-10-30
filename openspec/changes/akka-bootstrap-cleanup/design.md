## Context

Njord's `AkkaSetupContainer` has accumulated abstractions absent from the FunkArr reference: a `PersistenceSelection` record, an `AkkaLogLevel` Serilog parser, and a Port-0 self-join path. The telemetry stack uses prometheus-net while FunkArr uses OpenTelemetry SDK natively. This change aligns both.

Current state:
- `AkkaSetupContainer` delegates to `PersistenceSelection.From()`, `AkkaLogLevel.From()`, and has a port-0 branch with `Cluster.Get(system).Join(selfAddress)`
- `TelemetrySetupContainer` uses `Metrics.ConfigureMeterAdapter` (prometheus-net) to bridge .NET Meters
- `NjordApplicationSetup` calls `UseHttpMetrics()` and `MapMetrics()` from prometheus-net

## Goals / Non-Goals

**Goals:**
- AkkaSetupContainer matches FunkArr's direct, inline style
- Persistence detection is implicit (ConnectionString present = Postgres, absent = SQLite)
- Telemetry uses OpenTelemetry SDK natively like FunkArr
- Fewer files, fewer abstractions, same behavior

**Non-Goals:**
- Changing IActorRegistration pattern (it works, domain containers keep their registrations)
- Changing HttpMetricsHandler (it writes custom metrics on `Njord.OpenMeteo` meter, OTel collects them)
- Changing domain `Telemetry` classes (they use .NET `Meter`, OTel picks them up)
- Adding distributed tracing (future work)

## Decisions

### D1: Inline persistence selection (like FunkArr)

FunkArr checks `!string.IsNullOrEmpty(postgresOptions.Host)` to pick the provider. Njord will check `!string.IsNullOrEmpty(persistence.ConnectionString)`:

```
ConnectionString set   → PostgreSQL, use ConnectionString as-is
ConnectionString null  → SQLite, build "Data Source={Path.GetFullPath(DataPath)}"
```

**Why not keep the Provider enum?** It's a second source of truth. If someone sets `Provider=PostgreSql` but forgets `ConnectionString`, validation catches it -- but the simpler model makes the invalid state unrepresentable. FunkArr proves this works.

**Alternative considered**: Keep Provider enum but simplify. Rejected: adds no value over ConnectionString detection.

### D2: Drop AkkaLogLevel, use FunkArr's simple logging

FunkArr: `ClearLoggers()` + `AddLoggerFactory()`. That's it. Akka's internal log level follows whatever `Microsoft.Extensions.Logging` filters through from Serilog.

**Why not keep AkkaLogLevel?** It parses Serilog config to set `loggers.LogLevel` to the lowest override. In practice nobody debugs Akka internals in Njord, and the `AddLoggerFactory()` bridge already respects the ILoggerFactory filtering. The explicit level is redundant.

### D3: Hardcode port 2552 (like FunkArr)

FunkArr hardcodes `localhost:2552` for remoting and the seed node. Njord runs as a single-node cluster in Docker with its own network namespace -- there's no port conflict scenario.

**Why not keep configurable port?** The Port-0 branch adds `Akka.Cluster` import, a `Startup` callback, and conditional clustering config -- all for a scenario that never occurs. Remove it.

### D4: OpenTelemetry SDK replaces prometheus-net

FunkArr pattern:
```csharp
services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService("Njord", serviceVersion: version))
    .WithMetrics(m => m
        .AddAspNetCoreInstrumentation()
        .AddProcessInstrumentation()
        .AddRuntimeInstrumentation()
        .AddMeter("Njord.OpenMeteo")
        .AddMeter("Njord.Budget")
        .AddMeter("Njord.Pipeline")
        .AddMeter("Njord.Polling")
        .AddPrometheusExporter());
```

App pipeline: `app.MapPrometheusScrapingEndpoint()` replaces `app.UseHttpMetrics()` + `app.MapMetrics()`.

**Why OpenTelemetry over prometheus-net?** FunkArr already uses it. OTel is the CNCF standard. It gives us ASP.NET Core, process, and runtime instrumentation for free. The `NjordMeters` set becomes unnecessary -- each meter is registered by name.

### D5: Keep HttpMetricsHandler

The handler writes `njord_openmeteo_http_requests_total` and `njord_openmeteo_http_duration_seconds` on the `Njord.OpenMeteo` meter. These are domain-specific and not replaceable by `AddHttpClientInstrumentation()` (which uses different metric names and tags). Since we register `AddMeter("Njord.OpenMeteo")`, the handler's metrics flow through OTel unchanged.

## Risks / Trade-offs

- **[Config breaking change]** Users with `Njord:Persistence:Provider=PostgreSql` must remove it. Risk is low: they already have `ConnectionString` set, and Provider becomes redundant. Docker Compose example will be updated.
  - Mitigation: Clear error if unknown config keys are bound (standard .NET behavior); document in release notes.

- **[Metric name stability]** prometheus-net and OpenTelemetry may format metric names slightly differently (e.g. `_total` suffix handling). Existing Grafana dashboards or alerts may need adjustment.
  - Mitigation: Verify metric names match after migration. The Njord meters use explicit names (`njord_*`) that should pass through unchanged.

- **[Package churn]** Removing 2 packages, adding 6. Net increase in dependency count.
  - Mitigation: FunkArr already validates this package set works together.

## Migration Plan

1. Part 1 (Akka bootstrap): Delete 4 files, rewrite AkkaSetupContainer + PersistenceOptions + HealthSetupContainer. Update tests. Low risk, fully testable.
2. Part 2 (Telemetry): Swap packages, rewrite TelemetrySetupContainer + NjordApplicationSetup. Verify `/metrics` endpoint still works. Medium risk due to package swap.
3. Update docker-compose.example.yml and Config Builder if they reference Provider.
4. Run all test projects + verify `dotnet run` starts cleanly.

## Open Questions

(none -- decisions are straightforward FunkArr alignment)
