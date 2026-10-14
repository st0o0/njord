## Purpose

Structured logging with SourceContext-based attribution, level discipline, and operational visibility across all njord actors and services.
## Requirements
### Requirement: Actor logging via Akka ILoggingAdapter

All actor classes (types extending `ActorBase` or its subclasses) SHALL use `Context.GetLogger()` to obtain an `ILoggingAdapter` for logging. Actor classes SHALL NOT inject `ILogger<T>` via constructor for logging purposes. Every log event an actor emits through its `ILoggingAdapter` SHALL reach the host's logging pipeline with the actor's full type name as its log category, so that it carries that name as `SourceContext` in Serilog.

#### Scenario: Actor uses Akka logger
- **WHEN** `PollScheduler` emits a log message
- **THEN** the log is emitted via `ILoggingAdapter` obtained from `Context.GetLogger()`

#### Scenario: Actor log includes SourceContext
- **WHEN** `PollScheduler` emits a log message through `ILoggingAdapter`
- **THEN** the log event contains `SourceContext` with value `"Njord.Pipeline.PollScheduler"`, not `"Akka.Actor.ActorSystem"`

#### Scenario: Actor log keeps its structured properties
- **WHEN** `PollScheduler` logs `"Data changed for {Location}/{Model}"`
- **THEN** the log event carries the named properties `Location` and `Model`, the message template, and the `ActorPath` and `LogSource` of the emitting actor

### Requirement: Non-actor logging via ILogger

Non-actor classes (gRPC services such as `OpsGrpcService`, `MqttNetPublisher`) SHALL use `ILogger<T>` obtained via dependency injection for logging. These classes SHALL NOT reference `Serilog.Context` or any Serilog-specific API.

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
- **WHEN** a log event from `PollScheduler` at Information level is rendered
- **THEN** the console output contains `[INF] [Njord.Pipeline.PollScheduler]`

#### Scenario: Framework log line with SourceContext
- **WHEN** a framework log event from `Microsoft.AspNetCore.Hosting.Diagnostics` is rendered
- **THEN** the console output contains `[Microsoft.AspNetCore.Hosting.Diagnostics]`

### Requirement: Framework log level suppression

The following framework log sources SHALL be filtered to Warning or above in production configuration via Serilog `MinimumLevel.Override`:
- `System.Net.Http.HttpClient`
- `Grpc.AspNetCore.Server`
- `Microsoft.AspNetCore.Routing`

The source `Microsoft.AspNetCore.Hosting.Diagnostics` SHALL remain at Information to preserve startup messages.

Njord application namespaces (`Njord.OpenMeteo`, `Njord.Pipeline`, `Njord.Budget`, `Njord.SensorHub`, `Njord.Mqtt`, `Njord.Grpc`, `Njord.Configuration`) SHALL use the global minimum level by default and MAY be overridden individually in configuration.

#### Scenario: HttpClient logs suppressed in production
- **WHEN** the service polls 10 weather models in a single cycle
- **THEN** zero HttpClient Information-level log lines appear in the console output

#### Scenario: Startup messages preserved
- **WHEN** the application starts
- **THEN** "Now listening on" and "Application started" messages appear at Information level

#### Scenario: Per-namespace override
- **WHEN** `appsettings.json` sets `MinimumLevel.Override.Njord.Mqtt` to `Warning`
- **THEN** only Warning and above log events from `Njord.Mqtt.*` classes appear in console output

### Requirement: MQTT connected log

`MqttConnection` SHALL emit an Information-level log when a connection to the MQTT broker is successfully established, including the host and port.

#### Scenario: Successful MQTT connection
- **WHEN** `MqttConnection` successfully connects to the MQTT broker
- **THEN** an Information log `"MQTT connected to {Host}:{Port}"` is emitted

#### Scenario: Reconnection after disconnect
- **WHEN** `MqttConnection` reconnects after a connection loss
- **THEN** the same Information log is emitted again (not just on first connect)

### Requirement: Namespace override documentation for stream tracing

The example configuration (`appsettings.Example.json`) SHALL include commented examples showing how to enable Debug-level stream tracing per subsystem using Serilog `MinimumLevel.Override` entries. The override keys SHALL be log categories (the full type name of the actor that owns the graph), not `.Log()` stage names.

#### Scenario: Example config contains stream tracing overrides
- **WHEN** reading `appsettings.Example.json`
- **THEN** it contains a commented override entry for `Njord.Mqtt.MqttStatePublisher` with an explanation that it enables the `mqtt-state-in` and `mqtt-state-out` stages

### Requirement: Static helpers do not log
Static mapping and computation helpers (`StatePayloadBuilder`, `HorizonProjection`, `TopicScheme`, the analyzers' formula classes, the proto mapping extensions) SHALL NOT accept `ILogger` or `ILoggingAdapter` parameters and SHALL NOT log. Logging SHALL be performed by the calling actor or service. `StreamSupervision.LoggingDecider`, which builds a supervision decider from a logger, is the only static member that takes a logger.

#### Scenario: Payload builders do not log
- **WHEN** `MqttStatePublisher` maps an event with `StatePayloadBuilder`
- **THEN** the builder emits no log events; the actor's `.Log()` stages and logger do

### Requirement: SourceRef handshake logs at Debug level
`StreamConsumerActor` subclasses SHALL log the receipt of a SourceRef at Debug level, including the source actor path. Materializing the analysis pipeline in `ForecastPipeline` is a lifecycle transition and SHALL be logged at Information level.

#### Scenario: SourceRef received not visible at Information level
- **WHEN** `MqttStatePublisher` receives `RequestPipelineOutputCompleted` and the minimum log level is Information
- **THEN** no "SourceRef received" message appears in console output

#### Scenario: SourceRef received visible at Debug level
- **WHEN** `MqttStatePublisher` receives `RequestPipelineOutputCompleted` and the minimum log level is Debug
- **THEN** a Debug message `"Pipeline SourceRef received from {Source}"` with the sender's actor path appears

### Requirement: Debug logs carry concrete context
Debug-level logs SHALL include concrete contextual data beyond the bare event name: ref-received logs the source actor path, scheduling logs the location, model and absolute next-poll time, hash-unchanged logs the location, model, miss count and phase.

#### Scenario: Debug scheduling log includes next poll time
- **WHEN** `PollScheduler` schedules the next poll for a model
- **THEN** the Debug log `"Scheduling next poll for {Location}/{Model} at {NextPoll}"` carries the absolute UTC time

#### Scenario: Debug hash-unchanged log
- **WHEN** `PollScheduler` receives `FetchForecastCompleted` whose hash matches the previous value
- **THEN** a Debug log `"Hash unchanged for {Location}/{Model}"` with the miss count and phase is emitted

### Requirement: Poll cycle summary is logged at Debug level
`PollScheduler` SHALL track, per location, one outcome per resolved model of the current cycle: changed data, unchanged data, or failed. A `FetchForecastCompleted` SHALL count as changed or unchanged, and a `FetchForecastFailed` of any reason SHALL count as a final failed outcome for the current cycle; the cycle SHALL NOT wait for the retry of a failed model. A later outcome of a model whose cycle is still open SHALL replace that model's earlier outcome. When every resolved model of the location has an outcome, the cycle completes: `PollScheduler` SHALL record `njord_poll_cycle_duration_seconds`, `njord_poll_cycle_models` and `njord_poll_cycle_failed_models` and emit a Debug-level summary `"Poll complete for {Location}: {Changed}/{Total} models changed, {Failed} failed in {Duration}ms"`, and the next outcome SHALL open a new cycle. Outcomes for a location or model that is not configured SHALL open no cycle, so at most one cycle per configured location is open at any time. Failed fetches SHALL additionally be logged separately at Warning level.

#### Scenario: All models return data
- **WHEN** a poll cycle for location "vreden" completes with 10 models reporting and 3 of them changed
- **THEN** a Debug log `"Poll complete for vreden: 3/10 models changed, 0 failed in {Duration}ms"` is emitted

#### Scenario: Failed fetch is logged as warning
- **WHEN** a fetch for (vreden, icon_d2) fails with `Transport`
- **THEN** a Warning log naming the location, model and failure detail is emitted

#### Scenario: A cycle with a failed model completes
- **WHEN** location "lucerne" resolves models A and B, A reports changed data and B reports `FetchForecastFailed`
- **THEN** the cycle completes with 2 models, 1 changed and 1 failed, and a Debug log `"Poll complete for lucerne: 1/2 models changed, 1 failed in {Duration}ms"` is emitted

#### Scenario: Consecutive cycles with failures stay separate
- **WHEN** a cycle of models A, B and C completes with C failed, and afterwards A, B and C all report again
- **THEN** two cycles complete, each with 3 models, the first with 1 failed and the second with 0 failed

#### Scenario: A retry before completion replaces the failed outcome
- **WHEN** model A fails, A's retry succeeds while model B is still outstanding, and then B reports
- **THEN** the cycle completes with 2 models and 0 failed

#### Scenario: Outcomes for unknown models open no cycle
- **WHEN** a `FetchForecastFailed` arrives for a model that is not resolved for the location
- **THEN** no cycle is opened and no summary is emitted

### Requirement: Lifecycle transitions are logged at Information level
Operationally significant lifecycle events SHALL be logged at Information level, including `"OpenMeteoGateway stream materialized"` (`OpenMeteoGateway`), `"Gateway resolved - scheduling initial polls"` (`PollScheduler`), `"Received forecast SourceRef from gateway — materializing analysis pipeline"` (`ForecastPipeline`), `"MQTT connected to {Host}:{Port}"` (`MqttConnection`), `"Discovery interval changed from {Previous} to {Current}"` (`PollScheduler`) and the `ConfigManager` reload messages. Per-poll events such as `"Data changed for {Location}/{Model}"` SHALL be logged at Debug level.

#### Scenario: Gateway materialization stays at Information
- **WHEN** `OpenMeteoGateway` materializes its streams
- **THEN** `"OpenMeteoGateway stream materialized"` is logged at Information level

#### Scenario: Data changed is a Debug log
- **WHEN** `PollScheduler` detects changed data for a model
- **THEN** `"Data changed for {Location}/{Model}"` is logged at Debug level

### Requirement: Akka internal logs keep an Akka category

Log events emitted by Akka.NET itself (cluster, remoting, persistence, streams internals and log sources given as plain strings) SHALL keep a log category in the `Akka` namespace, so the production override `"Akka": "Warning"` filters them while it does not filter njord's own actors.

#### Scenario: Akka internal Information log is filtered in production
- **WHEN** an Akka component logs at Information level and the production configuration (`"Akka": "Warning"`) is active
- **THEN** the log event does not appear in the output

#### Scenario: njord actor Information log is visible in production
- **WHEN** a njord actor logs at Information level and the production configuration (default `Information`, `"Akka": "Warning"`) is active
- **THEN** the log event appears in the output with the actor's full type name as `SourceContext`

### Requirement: Akka log level follows the configured minimum level

The actor system's own log level SHALL be derived at startup from the Serilog minimum level configuration: the lowest of `Serilog:MinimumLevel:Default` (or `Serilog:MinimumLevel` given as a single level) and every `Serilog:MinimumLevel:Override` value, mapped to Akka's levels (`Verbose` and `Debug` to Debug, `Information` to Info, `Warning` to Warning, `Error` and `Fatal` to Error). Without a configured minimum level it SHALL be Info. Serilog SHALL remain the per-category filter, so an actor Debug log appears exactly when the level configured for its category allows Debug.

#### Scenario: Production keeps Akka at Info
- **WHEN** the configuration sets `Default` to `Information` and only `Warning` overrides
- **THEN** the actor system's log level is Info and no actor Debug log event is created

#### Scenario: A Debug override reaches actor Debug logs
- **WHEN** the configuration sets `Default` to `Information` and the override `"Njord.Pipeline": "Debug"`
- **THEN** the actor system's log level is Debug and a Debug log of `PollScheduler` appears in the output, while Debug logs of other categories do not

#### Scenario: Development Debug default
- **WHEN** the configuration sets `Default` to `Debug` and `"Akka": "Warning"`
- **THEN** njord actor Debug logs appear and Akka internal Debug and Information logs do not

