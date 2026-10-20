## Purpose

Structured logging with SourceContext-based attribution, level discipline, and operational visibility across all njord actors and services.
## Requirements
### Requirement: Actor logging via Akka ILoggingAdapter

All actor classes (types extending `ActorBase` or its subclasses) SHALL use `Context.GetLogger()` to obtain an `ILoggingAdapter` for logging. Actor classes SHALL NOT inject `ILogger<T>` via constructor for logging purposes. Every log event an actor emits through its `ILoggingAdapter` SHALL reach the host's logging pipeline with the actor's full type name as its log category, so that it carries that name as `SourceContext` in Serilog.

#### Scenario: Actor uses Akka logger
- **WHEN** `PollWorker` emits a log message
- **THEN** the log is emitted via `ILoggingAdapter` obtained from `Context.GetLogger()`

#### Scenario: Actor log includes SourceContext
- **WHEN** `PollWorker` emits a log message through `ILoggingAdapter`
- **THEN** the log event contains `SourceContext` with value `"Njord.Pipeline.PollWorker"`, not `"Akka.Actor.ActorSystem"`

#### Scenario: Actor log keeps its structured properties
- **WHEN** `PollWorker` logs `"Data changed for {Location}/{Model}"`
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
- **WHEN** a log event from `PollManager` at Information level is rendered
- **THEN** the console output contains `[INF] [Njord.Pipeline.PollManager]`

#### Scenario: Framework log line with SourceContext
- **WHEN** a framework log event from `Microsoft.AspNetCore.Hosting.Diagnostics` is rendered
- **THEN** the console output contains `[Microsoft.AspNetCore.Hosting.Diagnostics]`

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

### Requirement: Lifecycle transitions are logged at Information level
Operationally significant lifecycle events SHALL be logged at Information level, including `"OpenMeteoGateway stream materialized"` (`OpenMeteoGateway`), `"Starting {Count} poll workers"` (`PollManager`), `"Received forecast SourceRef from gateway — materializing analysis pipeline"` (`ForecastPipeline`), `"MQTT connected to {Host}:{Port}"` (`MqttConnection`), `"Discovery interval changed from {Previous} to {Current}"` (`PollManager`) and the `ConfigManager` reload messages. Per-poll events such as `"Data changed for {Location}/{Model}"` (`PollWorker`) SHALL be logged at Debug level.

#### Scenario: Gateway materialization stays at Information
- **WHEN** `OpenMeteoGateway` materializes its streams
- **THEN** `"OpenMeteoGateway stream materialized"` is logged at Information level

#### Scenario: Data changed is a Debug log
- **WHEN** a `PollWorker` detects changed data for its target
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
- **THEN** the actor system's log level is Debug and a Debug log of `PollWorker` appears in the output, while Debug logs of other categories do not

#### Scenario: Development Debug default
- **WHEN** the configuration sets `Default` to `Debug` and `"Akka": "Warning"`
- **THEN** njord actor Debug logs appear and Akka internal Debug and Information logs do not

