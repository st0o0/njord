## Why

Production logs are noisy and hard to read. Framework logging (HttpClient, gRPC routing, Kestrel) dominates output with ~40 lines per poll cycle. Njord's own logs lack source attribution (no way to tell which actor emitted "Pipeline SourceRef received"), use Information level for internal plumbing that should be Debug, and leave important subsystems (enrichment, MQTT egress) completely silent. Debug-level logs carry too little context to be useful for troubleshooting.

## What Changes

- **Framework noise suppression**: Filter `System.Net.Http.HttpClient`, `Grpc.AspNetCore.Server`, and `Microsoft.AspNetCore.Routing` to Warning in appsettings. Keep `Microsoft.AspNetCore.Hosting` at Information for startup messages.
- **Subsystem property per actor**: Each actor sets an explicit `Subsystem` string property (e.g. `"pipeline"`, `"egress"`, `"mqtt"`, `"enrich"`, `"grpc"`) via Serilog `LogContext.PushProperty` in the constructor. Non-actor services (gRPC services, `MqttNetPublisher`) set it the same way.
- **Output template with subsystem tag**: Change the Serilog console template to include `[{Subsystem}]` between level and message.
- **Level reassignment**: Demote ref-received and graph-materialized plumbing logs from Information to Debug. Keep operationally relevant state transitions (pipeline connected, data changed, capability learned) at Information.
- **New Information-level logs**: Add logs for events that are currently silent — MQTT connected, enrichment computed, poll cycle summary.
- **Richer Debug logs**: Add context to Debug-level logs so they're actually useful — scheduling details with intervals, hash comparison results, MQTT message counts, ref types received.

## Capabilities

### New Capabilities

- `structured-logging`: Subsystem property enrichment, output template, level assignments, and new log statements across all actors and services.

### Modified Capabilities

_(none — no spec-level behavior changes, only observability improvements)_

## Impact

- **Files changed**: `Program.cs` (template), `appsettings.json` / `appsettings.Development.json` (log levels), all actor classes (subsystem property + level changes + new log statements), `StreamSupervision.cs`.
- **No API changes**: Pure observability, no functional behavior change.
- **No new dependencies**: Uses existing Serilog `LogContext` and `ILogger<T>`.
- **Test impact**: `StreamSupervisionSpec` asserts on log levels — needs update for any level changes there.

## Non-goals

- Switching logger infrastructure (staying with Serilog + `ILogger<T>`).
- Migrating actors from `ILogger<T>` to Akka's `Context.GetLogger()` or vice versa — the current split stays.
- Adding structured sinks (Seq, Loki, etc.) — that's a separate concern.
- Trace-level logging or payload dumping.
- Correlation IDs or distributed tracing enrichment (already handled by Servus `IWithTracing`).
