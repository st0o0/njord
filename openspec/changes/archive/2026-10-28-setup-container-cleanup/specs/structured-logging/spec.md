## MODIFIED Requirements

### Requirement: Actor logging via Akka ILoggingAdapter

All actor classes (types extending `ActorBase` or its subclasses) SHALL use `Context.GetLogger()` to obtain an `ILoggingAdapter` for logging. Actor classes SHALL NOT inject `ILogger<T>` via constructor for logging purposes. Actor log events SHALL reach the host's logging pipeline through `AddLoggerFactory()` (Akka.Hosting's `LoggerFactoryLogger`), which routes them to `ILogger<ActorSystem>`. The `LogSource` property on each log event SHALL carry the actor's path for operational filtering.

#### Scenario: Actor uses Akka logger
- **WHEN** `PollWorker` emits a log message
- **THEN** the log is emitted via `ILoggingAdapter` obtained from `Context.GetLogger()`

#### Scenario: Actor log SourceContext is ActorSystem
- **WHEN** `PollWorker` emits a log message through `ILoggingAdapter`
- **THEN** the log event contains `SourceContext` with value `"Akka.Actor.ActorSystem"` (the standard Akka.Hosting category)

#### Scenario: Actor log carries LogSource for filtering
- **WHEN** `PollWorker` emits a log message through `ILoggingAdapter`
- **THEN** the log event carries a `LogSource` property with the actor's path, usable for operational filtering

#### Scenario: Actor log keeps its structured properties
- **WHEN** `PollWorker` logs `"Data changed for {Location}/{Model}"`
- **THEN** the log event carries the named properties `Location` and `Model`, and the `ActorPath` of the emitting actor

## REMOVED Requirements

### Requirement: Akka internal logs keep an Akka category
**Reason**: With `AddLoggerFactory()`, all Akka logs (both actor and internal) flow through `ILogger<ActorSystem>`. Per-type category routing was provided by the custom `ActorCategoryLogger` which is being removed for simplicity. The `"Akka": "Warning"` Serilog override still filters Akka internals because `ActorSystem` lives in the `Akka.Actor` namespace.
**Migration**: Serilog overrides that targeted specific njord actor type names as categories will no longer filter actor logs. Use `LogSource`-based filtering or Serilog's property-based filters instead.
