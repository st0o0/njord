## Why

njord runs on a home server with no OTLP collector. The entire OpenTelemetry stack (tracing, metrics, OTLP export) is dead code — instruments are created but traces and metrics go nowhere. Serilog console logging is sufficient for operational visibility. Removing this simplifies the dependency tree and reduces noise in actor code.

## What Changes

- **Remove `NjordTelemetry` static class** (`ActivitySource`, `Meter`, all 10 instruments) and its test class
- **Remove all `NjordTelemetry.*` call-sites** in `SchedulerActor`, `PipelineActor`, `MqttConnectionActor`, `DiscoveryActor` (counter increments, `StartActivity` spans, duration recordings)
- **Strip OpenTelemetry setup** from `ServiceDefaults/Extensions.cs` (tracing, metrics, OTLP exporter) — keep Serilog with Console sink and health endpoints
- **Remove OpenTelemetry NuGet packages** (OTLP exporter, Extensions.Hosting, Instrumentation.AspNetCore, Instrumentation.Http, Serilog.Sinks.OpenTelemetry) and the `OpenTelemetry.Api` version override from `Directory.Packages.props`

## Non-goals

- Removing Serilog or structured console logging
- Removing the `ServiceDefaults` project (it still owns Serilog setup and health endpoints)
- Removing the `/healthz` or `/alive` endpoints
- Re-adding telemetry later (if needed, that would be a separate change)

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

- `telemetry-infrastructure`: removing all OpenTelemetry tracing/metrics setup; only Serilog console logging remains
- `pipeline-instrumentation`: removing all metric/tracing instrumentation from actors; actors no longer emit counters, histograms, or spans

## Impact

- **Code**: 4 actor files lose `NjordTelemetry.*` calls; `Extensions.cs` loses OTel setup; `NjordTelemetry.cs` + test deleted
- **Dependencies**: 5 OpenTelemetry packages + 1 Serilog sink removed from `ServiceDefaults.csproj` and `Directory.Packages.props`
- **Runtime**: no functional change — the removed code had no working backend
- **API budget**: no impact (no polling changes)
