## Context

njord currently has a full OpenTelemetry stack: `NjordTelemetry` (static class with `ActivitySource`, `Meter`, 10 instruments), OTel SDK setup in `ServiceDefaults/Extensions.cs`, and call-sites in 4 actors. The OTLP exporter is opt-in via env var, but no collector exists on the target home server — all instruments are no-ops at runtime.

Serilog with console sink provides all operational visibility needed. Health endpoints (`/healthz`, `/alive`) remain useful for Docker health checks.

## Goals / Non-Goals

**Goals:**
- Remove all OpenTelemetry tracing and metrics code (instruments, SDK setup, packages)
- Remove `NjordTelemetry` class and its test
- Clean up `ServiceDefaults` to only contain Serilog + health endpoints
- Remove unused NuGet packages from `Directory.Packages.props`

**Non-Goals:**
- Changing Serilog configuration or log output format
- Removing the `ServiceDefaults` project
- Removing health check endpoints
- Adding alternative observability (if needed later, separate change)

## Decisions

### Keep `AddNjordTelemetry()` method name, reduce its scope
The method stays as the Serilog setup entry point (renamed would churn callers). It loses all OTel code paths: `AddOpenTelemetry()`, `WithTracing()`, `WithMetrics()`, `UseOtlpExporter()`, and the `Serilog.Sinks.OpenTelemetry` conditional sink. Only Serilog with console sink remains.

**Alternative**: Rename to `AddNjordLogging()`. Rejected — unnecessary churn for a name-only change; can rename later if more setup methods appear.

### Remove `NjordTelemetry.cs` entirely, don't leave a stub
No stub class, no empty `ActivitySource`. The class exists only to hold OTel instruments; without OTel, it has no purpose.

**Alternative**: Keep the class with just the `ServiceName` constant. Rejected — `"njord"` is already a string literal in `Extensions.cs`; a class for one constant is noise.

### Remove call-sites inline, don't extract to a logging replacement
The `NjordTelemetry.*` calls in actors (counter increments, `StartActivity`) are deleted without replacement. The actors already have `ILogger` for operational visibility. Adding `_logger.LogDebug(...)` at every former counter site would be log spam.

**Alternative**: Replace counters with structured log events. Rejected — these were metrics (counts, durations), not events. Logging them would be noisy and serve no purpose without aggregation.

### Remove the Serilog OTLP sink
`Serilog.Sinks.OpenTelemetry` is only used when `OTEL_EXPORTER_OTLP_ENDPOINT` is set, which never happens. Remove the package and the conditional sink setup.

### Remove the `OpenTelemetry.Api` version override
The override in `Directory.Packages.props` exists to suppress NU1902 from `Akka.Hosting`'s transitive pull. After removing the direct OTel packages, check if the transitive reference still triggers the advisory. If so, keep the override; if not, remove it.

## Risks / Trade-offs

- **[Loss of future instrumentation hooks]** → If OTel is needed later, re-adding is straightforward (a new change). The current code provides no value without a backend.
- **[Akka.Hosting transitive OTel.Api]** → Akka.Hosting pulls `OpenTelemetry.Api` transitively. After removing direct OTel packages, the transitive version may trigger NU1902 again. Mitigation: check after package removal, keep the `OpenTelemetry.Api` pin if needed.
