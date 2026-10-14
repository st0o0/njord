## MODIFIED Requirements

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

### Requirement: Debug logs carry concrete context
Debug-level logs SHALL include concrete contextual data beyond the bare event name: ref-received logs the source actor path, scheduling logs the location, model and absolute next-poll time, hash-unchanged logs the location, model, miss count and phase.

#### Scenario: Debug scheduling log includes next poll time
- **WHEN** a `PollWorker` schedules the next poll for its target
- **THEN** the Debug log `"Scheduling next poll for {Location}/{Model} at {NextPoll}"` carries the absolute UTC time

#### Scenario: Debug hash-unchanged log
- **WHEN** a `PollWorker` receives `FetchForecastCompleted` whose hash matches the previous value
- **THEN** a Debug log `"Hash unchanged for {Location}/{Model}"` with the miss count and phase is emitted

### Requirement: Poll cycle summary is logged at Debug level
`PollManager` SHALL track, per location, one outcome per resolved model of the current cycle from the workers' reports: changed data, unchanged data, or failed. A changed or unchanged report SHALL count as that outcome, and a failed report (a `FetchForecastFailed` of any reason, a rejection or an unanswered request) SHALL count as a final failed outcome for the current cycle; the cycle SHALL NOT wait for the retry of a failed model. A planned report (start or recovery) SHALL count as no outcome. A later outcome of a model whose cycle is still open SHALL replace that model's earlier outcome. When every resolved model of the location has an outcome, the cycle completes: `PollManager` SHALL record `njord_poll_cycle_duration_seconds`, `njord_poll_cycle_models` and `njord_poll_cycle_failed_models` and emit a Debug-level summary `"Poll complete for {Location}: {Changed}/{Total} models changed, {Failed} failed in {Duration}ms"`, and the next outcome SHALL open a new cycle. Reports for a location or model that is not configured SHALL open no cycle, so at most one cycle per configured location is open at any time. Failed fetches SHALL additionally be logged separately at Warning level by the worker.

#### Scenario: All models return data
- **WHEN** a poll cycle for location "vreden" completes with 10 models reporting and 3 of them changed
- **THEN** a Debug log `"Poll complete for vreden: 3/10 models changed, 0 failed in {Duration}ms"` is emitted

#### Scenario: Failed fetch is logged as warning
- **WHEN** a fetch for (vreden, icon_d2) fails with `Transport`
- **THEN** a Warning log naming the location, model and failure detail is emitted

#### Scenario: A cycle with a failed model completes
- **WHEN** location "lucerne" resolves models A and B, A reports changed data and B reports a failure
- **THEN** the cycle completes with 2 models, 1 changed and 1 failed, and a Debug log `"Poll complete for lucerne: 1/2 models changed, 1 failed in {Duration}ms"` is emitted

#### Scenario: Consecutive cycles with failures stay separate
- **WHEN** a cycle of models A, B and C completes with C failed, and afterwards A, B and C all report again
- **THEN** two cycles complete, each with 3 models, the first with 1 failed and the second with 0 failed

#### Scenario: A retry before completion replaces the failed outcome
- **WHEN** model A fails, A's retry succeeds while model B is still outstanding, and then B reports
- **THEN** the cycle completes with 2 models and 0 failed

#### Scenario: Outcomes for unknown models open no cycle
- **WHEN** a failed report arrives for a model that is not resolved for the location
- **THEN** no cycle is opened and no summary is emitted

### Requirement: Lifecycle transitions are logged at Information level
Operationally significant lifecycle events SHALL be logged at Information level, including `"OpenMeteoGateway stream materialized"` (`OpenMeteoGateway`), `"Starting {Count} poll workers"` (`PollManager`), `"Received forecast SourceRef from gateway — materializing analysis pipeline"` (`ForecastPipeline`), `"MQTT connected to {Host}:{Port}"` (`MqttConnection`), `"Discovery interval changed from {Previous} to {Current}"` (`PollManager`) and the `ConfigManager` reload messages. Per-poll events such as `"Data changed for {Location}/{Model}"` (`PollWorker`) SHALL be logged at Debug level.

#### Scenario: Gateway materialization stays at Information
- **WHEN** `OpenMeteoGateway` materializes its streams
- **THEN** `"OpenMeteoGateway stream materialized"` is logged at Information level

#### Scenario: Data changed is a Debug log
- **WHEN** a `PollWorker` detects changed data for its target
- **THEN** `"Data changed for {Location}/{Model}"` is logged at Debug level
