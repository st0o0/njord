## MODIFIED Requirements

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

### Requirement: Namespace override documentation for stream tracing

The example configuration (`appsettings.Example.json`) SHALL include commented examples showing how to enable Debug-level stream tracing per subsystem using Serilog `MinimumLevel.Override` entries. The override keys SHALL be log categories (the full type name of the actor that owns the graph), not `.Log()` stage names.

#### Scenario: Example config contains stream tracing overrides
- **WHEN** reading `appsettings.Example.json`
- **THEN** it contains a commented override entry for `Njord.Mqtt.MqttStatePublisher` with an explanation that it enables the `mqtt-state-in` and `mqtt-state-out` stages

## ADDED Requirements

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
