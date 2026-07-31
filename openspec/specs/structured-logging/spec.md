## Purpose

Structured logging with SourceContext-based attribution, level discipline, and operational visibility across all njord actors and services.

## Requirements

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

### Requirement: Plumbing logs at Debug level

Ref-received and graph-materialized logs that indicate internal wiring SHALL be emitted at Debug level, not Information. These include: SinkRef received, SourceRef received, refs received/connecting, gRPC snapshot consumer materialized.

#### Scenario: SinkRef received not visible at Information level
- **WHEN** `ModelStateActor` receives an `EgressSinkResponse` and the minimum log level is Information
- **THEN** no "SinkRef received" message appears in console output

#### Scenario: SinkRef received visible at Debug level
- **WHEN** `ModelStateActor` receives an `EgressSinkResponse` and the minimum log level is Debug
- **THEN** a Debug-level message appears with the ref type and source actor path

### Requirement: Enriched Debug log content

Debug-level logs SHALL include concrete contextual data beyond the bare event name. Specifically:

- Ref-received logs SHALL include the ref type and source actor path.
- Scheduling logs SHALL include the location, model, and absolute next-poll time.
- Hash-unchanged logs SHALL include location and model.
- MQTT publish logs SHALL include the message count and location.

#### Scenario: Debug ref-received log includes source
- **WHEN** `EnrichmentActor` receives a `PipelineSourceResponse` at Debug level
- **THEN** the log message includes the type name `"PipelineSourceResponse"` and the sender actor path

#### Scenario: Debug scheduling log includes next poll time
- **WHEN** `SchedulerActor` schedules the next poll for a model
- **THEN** the Debug log includes `{Location}`, `{Model}`, and `{NextPoll}` as an absolute UTC time

#### Scenario: Debug hash-unchanged log
- **WHEN** `SchedulerActor` receives a `HashResult` where the hash matches the previous value
- **THEN** a Debug log `"Hash unchanged for {Location}/{Model}"` is emitted

### Requirement: Poll cycle summary log

`SchedulerActor` SHALL emit an Information-level log summarizing each poll cycle after all model results for a location have been processed or timed out.

The message SHALL include: location, number of models with changed data, total models polled, and total duration in milliseconds.

#### Scenario: All models return data
- **WHEN** a poll cycle for location "vreden" completes with 10 models polled and 3 reporting changed data
- **THEN** an Information log is emitted: `"Poll complete for vreden: 3/10 models changed in {Duration}ms"`

#### Scenario: Some models fail
- **WHEN** a poll cycle completes with 8 of 10 models returning data and 2 failing
- **THEN** the summary log reports `8/10` and the 2 failures are logged separately as Warning

### Requirement: MQTT connected log

`MqttConnectionActor` SHALL emit an Information-level log when a connection to the MQTT broker is successfully established, including the host and port.

#### Scenario: Successful MQTT connection
- **WHEN** `MqttConnectionActor` successfully connects to the MQTT broker
- **THEN** an Information log `"MQTT connected to {Host}:{Port}"` is emitted

#### Scenario: Reconnection after disconnect
- **WHEN** `MqttConnectionActor` reconnects after a connection loss
- **THEN** the same Information log is emitted again (not just on first connect)

### Requirement: Enrichment computed log

`EnrichmentActor` SHALL emit an Information-level log when enrichment features have been computed for a location, listing which features ran.

#### Scenario: All enrichment features compute
- **WHEN** enrichment computes for location "vreden" with consensus, alerts, trends, indices, energy, and history enabled
- **THEN** an Information log `"Enrichment computed for vreden: consensus, alerts, trends, indices, energy, history"` is emitted

### Requirement: State transitions remain at Information level

The following operationally significant events SHALL remain at Information level:
- `"Pipeline graph materialized"` (PipelineActor)
- `"Pipeline connection established"` (SchedulerActor)
- `"Data changed for {Location}/{Model}"` (SchedulerActor)
- `"Capability learned for {Location}/{Model}"` (ModelStateActor)
- `"MQTT discovery disabled — idle"` (DiscoveryActor)
- `"DiscoveryActor ready"` (DiscoveryActor)
- Discovery published/updated events (DiscoveryActor)
- `"TriggerImmediatePoll"` (SchedulerActor)

#### Scenario: Data changed stays at Information
- **WHEN** `SchedulerActor` detects changed data for a model
- **THEN** the `"Data changed for {Location}/{Model}"` log is emitted at Information level

#### Scenario: Capability learned stays at Information
- **WHEN** `ModelStateActor` learns capabilities for a new model
- **THEN** the `"Capability learned"` log is emitted at Information level

### Requirement: Namespace override documentation for stream tracing

The example configuration (`appsettings.Example.json`) SHALL include commented examples showing how to enable Debug-level stream tracing per subsystem using Serilog `MinimumLevel.Override` entries.

#### Scenario: Example config contains stream tracing overrides
- **WHEN** reading `appsettings.Example.json`
- **THEN** it contains commented override entries for stream log stage names (e.g., `pipeline-fetch-in`, `mqtt-send`) with an explanation of their purpose
