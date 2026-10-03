## REMOVED Requirements

### Requirement: Subsystem property on all log events
**Reason**: Replaced by built-in `SourceContext` property. Manual `Subsystem` enrichment required boilerplate scope management in every class and introduced Serilog API coupling in application code. `SourceContext` is set automatically by both `ILogger<T>` and `Akka.Logger.Serilog`, providing more granular per-class attribution.
**Migration**: Remove all `LogContext.PushProperty("Subsystem", ...)`, `.WithContext("Subsystem", ...)`, and associated `_logScope` fields/disposal. Use Serilog `MinimumLevel.Override` with namespace paths (e.g., `Njord.Pipeline`) for filtering.

### Requirement: Console output template includes subsystem
**Reason**: Replaced by SourceContext-based template. The `Subsystem` property no longer exists.
**Migration**: Change output template from `[{Subsystem,-8}]` to `[{SourceContext}]`.

## MODIFIED Requirements

### Requirement: Framework log level suppression

The following framework log sources SHALL be filtered to Warning or above in production configuration via Serilog `MinimumLevel.Override`:
- `System.Net.Http.HttpClient`
- `Grpc.AspNetCore.Server`
- `Microsoft.AspNetCore.Routing`

The source `Microsoft.AspNetCore.Hosting.Diagnostics` SHALL remain at Information to preserve startup messages.

Njord application namespaces (`Njord.Pipeline`, `Njord.Mqtt`, `Njord.Grpc`, `Njord.Egress`, `Njord.Enrichment`) SHALL use the global minimum level by default and MAY be overridden individually in configuration.

#### Scenario: HttpClient logs suppressed in production
- **WHEN** the service polls 10 weather models in a single cycle
- **THEN** zero HttpClient Information-level log lines appear in the console output

#### Scenario: Startup messages preserved
- **WHEN** the application starts
- **THEN** "Now listening on" and "Application started" messages appear at Information level

#### Scenario: Per-namespace override
- **WHEN** `appsettings.json` sets `MinimumLevel.Override.Njord.Mqtt` to `Warning`
- **THEN** only Warning and above log events from `Njord.Mqtt.*` classes appear in console output

## ADDED Requirements

### Requirement: Actor logging via Akka ILoggingAdapter

All actor classes (types extending `ActorBase` or its subclasses) SHALL use `Context.GetLogger()` to obtain an `ILoggingAdapter` for logging. Actor classes SHALL NOT inject `ILogger<T>` via constructor for logging purposes.

#### Scenario: Actor uses Akka logger
- **WHEN** `SchedulerActor` emits a log message
- **THEN** the log is emitted via `ILoggingAdapter` obtained from `Context.GetLogger()`

#### Scenario: Actor log includes SourceContext
- **WHEN** `SchedulerActor` emits a log message through `ILoggingAdapter`
- **THEN** the log event contains `SourceContext` with value `"Njord.Pipeline.SchedulerActor"` (set by the Akka.Logger.Serilog bridge)

### Requirement: Non-actor logging via ILogger

Non-actor classes (gRPC services, `MqttNetPublisher`, `HistoryEnrichment`) SHALL use `ILogger<T>` obtained via dependency injection for logging. These classes SHALL NOT reference `Serilog.Context` or any Serilog-specific API.

#### Scenario: gRPC service uses ILogger
- **WHEN** `OpsGrpcService` emits a log message
- **THEN** the log is emitted via `ILogger<OpsGrpcService>` obtained from DI

#### Scenario: Non-actor log includes SourceContext
- **WHEN** `OpsGrpcService` emits a log message through `ILogger<OpsGrpcService>`
- **THEN** the log event contains `SourceContext` with value `"Njord.Grpc.OpsGrpcService"`

### Requirement: No Serilog imports in application code

Application code (all files except `Program.cs`) SHALL NOT contain `using Serilog` or `using Serilog.Context` directives. Serilog SHALL be a pure infrastructure concern configured only in `Program.cs`.

#### Scenario: No Serilog using directives in actors
- **WHEN** scanning all `.cs` files under `src/Njord/` excluding `Program.cs`
- **THEN** no file contains a `using Serilog` or `using Serilog.Context` directive

### Requirement: Console output template uses SourceContext

The Serilog console output template SHALL include the `SourceContext` property between the log level and the message.

#### Scenario: Formatted log line with SourceContext
- **WHEN** a log event from `SchedulerActor` at Information level is rendered
- **THEN** the console output contains `[INF] [Njord.Pipeline.SchedulerActor]`

#### Scenario: Framework log line with SourceContext
- **WHEN** a framework log event from `Microsoft.AspNetCore.Hosting.Diagnostics` is rendered
- **THEN** the console output contains `[Microsoft.AspNetCore.Hosting.Diagnostics]`

### Requirement: Static methods do not accept loggers

Static stream-builder methods SHALL NOT accept `ILogger` or `ILoggingAdapter` parameters for logging. Logging SHALL be performed by the calling actor after the method returns.

#### Scenario: BuildCapabilityLearned does not log
- **WHEN** `ModelStateActor` calls the static `BuildCapabilityLearned` method
- **THEN** the method does not emit any log events; the actor logs the result

#### Scenario: BuildConsensusInlineFlow does not log
- **WHEN** `EnrichmentActor` calls the static `BuildConsensusInlineFlow` method
- **THEN** the method does not emit any log events; the actor logs the result
