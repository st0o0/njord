## Why

Logging in njord uses three different APIs: Akka's `ILoggingAdapter`, Microsoft's `ILogger<T>`, and direct Serilog `LogContext` calls. All funnel into Serilog, but the inconsistency causes a custom `Subsystem` property with manual scope management (`PushProperty`/`Dispose`) across 14 files — including two scope leaks in gRPC services. Standardizing on the right logger per context (Akka in actors, `ILogger<T>` elsewhere) and dropping the custom property in favor of the built-in `SourceContext` eliminates boilerplate, fixes the leaks, and makes log filtering more granular (per-class instead of per-subsystem).

## What Changes

- **Actors → Akka `ILoggingAdapter`**: Migrate 8 actors from `ILogger<T>` to `Context.GetLogger()`. The 4 actors already using Akka's logger drop their `.WithContext("Subsystem", ...)` call.
- **Non-actors keep `ILogger<T>`**: gRPC services, `MqttNetPublisher`, and `HistoryEnrichment` stay on `ILogger<T>` via DI — no Serilog imports needed.
- **Drop `Subsystem` property**: Remove all `LogContext.PushProperty("Subsystem", ...)` and `.WithContext("Subsystem", ...)` calls. Remove `_logScope` fields and their `Dispose` plumbing (12 sites).
- **Use `SourceContext` in output template**: Change the Serilog console template from `[{Subsystem,-8}]` to `[{SourceContext}]`. `ILogger<T>` sets this automatically; `Akka.Logger.Serilog` sets it to the actor type name.
- **Move logging out of static stream-builder methods**: `ModelStateActor.BuildCapabilityLearned` and `EnrichmentActor.BuildConsensusInlineFlow` currently accept an `ILogger` parameter and log from inside. Move the log call to the calling actor so the static methods become pure builders.
- **Remove `Serilog.Context` imports from application code**: Serilog becomes a pure infrastructure concern in `Program.cs`.
- **Update structured-logging spec**: Replace the `Subsystem` requirement with `SourceContext`-based identification. Update the output template scenario. Subsystem-based filtering becomes namespace-based Serilog `Override` configuration.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `structured-logging`: Replace `Subsystem` property requirement with `SourceContext`-based log attribution. Update console output template requirement. Remove subsystem-specific scenarios and add SourceContext-based equivalents.

## Impact

- **14 production files** modified (logging API changes, scope removal)
- **`Program.cs`** output template change
- **`appsettings*.json`** files: replace `Subsystem`-based filtering examples with `MinimumLevel.Override` by namespace
- **Tests**: any tests asserting on `Subsystem` property or log output format need updating
- **No API budget impact** — no polling changes
- **No breaking changes** — log output format changes but no external contracts affected

## Non-goals

- Changing the Serilog sink (console-only is fine for a Docker container with `docker logs`).
- Adding file/seq/OTLP sinks.
- Introducing log correlation IDs or distributed tracing spans.
- Changing log levels or adding/removing log statements beyond what's needed for the migration.
