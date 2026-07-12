## ADDED Requirements

### Requirement: The pipeline graph is materialized by an actor
A `PipelineActor` SHALL materialize the full pipeline graph using `Context.Materializer()`. The stream lifecycle SHALL be bound to the actor — when the actor stops, the graph terminates. No `IHostedService` or manual `KillSwitch` SHALL be used for pipeline lifecycle.

#### Scenario: Actor stop terminates the pipeline
- **WHEN** the PipelineActor is stopped
- **THEN** the pipeline graph completes and no further commands are processed

#### Scenario: Actor restart rematerializes the pipeline
- **WHEN** the PipelineActor restarts after a failure
- **THEN** a new pipeline graph is materialized and command processing resumes

### Requirement: The pipeline actor obtains the egress SinkRef before materializing
The PipelineActor SHALL request a `SinkRef<MqttMessage>` from the egress actor in `PreStart`. The actor SHALL stash all incoming messages until the SinkRef is received. Only after obtaining the SinkRef SHALL the pipeline graph be materialized.

#### Scenario: Pipeline waits for egress readiness
- **WHEN** the pipeline actor starts and the egress actor has not yet responded
- **THEN** the pipeline does not materialize its graph and stashes any incoming commands

#### Scenario: SinkRef received triggers materialization
- **WHEN** the egress actor responds with a SinkRef
- **THEN** the pipeline graph is materialized with the SinkRef as its terminal sink and stashed messages are unstashed

### Requirement: The pipeline maps FetchOutcome to MqttMessage at its terminal stage
The pipeline graph's final stage before the SinkRef sink SHALL map each `FetchOutcome.Success` to an `MqttMessage` containing the state topic, built state payload, and `retain: true`. `FetchOutcome.Failure` SHALL be logged and filtered out (not sent to the egress).

#### Scenario: Successful fetch becomes an MqttMessage
- **WHEN** a fetch for (lucerne, icon_d2) succeeds
- **THEN** the pipeline emits `MqttMessage("njord/lucerne/icon_d2/state", stateJson, true)` into the SinkRef

#### Scenario: Failed fetch is filtered
- **WHEN** a fetch fails
- **THEN** no MqttMessage is emitted; a warning is logged

### Requirement: The pipeline actor watches the egress actor for lifecycle coordination
The PipelineActor SHALL watch the egress actor. If the egress actor terminates (`Terminated` message), the pipeline actor SHALL stop its current graph and re-request a new SinkRef (triggering rematerialization once the egress is back).

#### Scenario: Egress restart triggers pipeline rematerialization
- **WHEN** the egress actor restarts and the pipeline receives `Terminated`
- **THEN** the pipeline actor stops its current graph, re-sends `RequestEgressSink`, stashes commands, and rematerializes when the new SinkRef arrives
