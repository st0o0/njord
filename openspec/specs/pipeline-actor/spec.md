# pipeline-actor Specification

## Purpose

Defines the PipelineActor that owns the pipeline stream graph lifecycle: actor-bound materialization, SinkRef coordination with the egress actor, Source.Queue exposure for the SchedulerActor, FetchOutcome-to-MqttMessage mapping with hash feedback, and egress watch for lifecycle recovery.

## Requirements

### Requirement: The pipeline graph is materialized by an actor
A `PipelineActor` SHALL materialize the full pipeline graph using `Context.Materializer()`. The stream lifecycle SHALL be bound to the actor — when the actor stops, the graph terminates. No `IHostedService` or manual `KillSwitch` SHALL be used for pipeline lifecycle.

#### Scenario: Actor stop terminates the pipeline
- **WHEN** the PipelineActor is stopped
- **THEN** the pipeline graph completes and no further commands are processed

#### Scenario: Actor restart rematerializes the pipeline
- **WHEN** the PipelineActor restarts after a failure
- **THEN** a new pipeline graph is materialized and command processing resumes

### Requirement: The pipeline actor obtains the egress SinkRef before materializing
The PipelineActor SHALL request a `SinkRef<MqttMessage>` from the egress actor in `PreStart`. The actor SHALL stash all incoming messages until the SinkRef is received. Only after obtaining the SinkRef SHALL the pipeline graph be materialized. After materialization, the actor SHALL expose a Source.Queue handle so the SchedulerActor can push `WeightedTarget` elements into the stream.

#### Scenario: Pipeline waits for egress readiness
- **WHEN** the pipeline actor starts and the egress actor has not yet responded
- **THEN** the pipeline does not materialize its graph and stashes any incoming commands

#### Scenario: SinkRef received triggers materialization and queue exposure
- **WHEN** the egress actor responds with a SinkRef
- **THEN** the pipeline graph is materialized with a Source.Queue entry point, and the queue handle is made available for the SchedulerActor

#### Scenario: SchedulerActor requests and receives the queue handle
- **WHEN** the SchedulerActor sends a `RequestPipelineQueue` message after materialization
- **THEN** the PipelineActor responds with the Source.Queue handle

### Requirement: The pipeline maps FetchOutcome to MqttMessage at its terminal stage
The pipeline graph's publish stage SHALL map each `FetchOutcome.Success` to `MqttMessage`(s) containing the state topic, built state payload, and `retain: true`. `FetchOutcome.Failure` SHALL be logged and filtered out. After publishing, the pipeline SHALL compute a hash over the forecast data and send a `HashResult` to the SchedulerActor via the built-in Ask flow. The terminal sink is `Sink.Ignore`.

#### Scenario: Successful fetch is published then hashed
- **WHEN** a fetch for (lucerne, icon_d2) succeeds
- **THEN** MqttMessages are published to the egress, then a `HashResult` is sent to the SchedulerActor via Ask, then the stream element is consumed by Sink.Ignore

#### Scenario: Failed fetch is filtered
- **WHEN** a fetch fails
- **THEN** no MqttMessage is emitted; a warning is logged; no HashResult is sent

### Requirement: The pipeline actor watches the egress actor for lifecycle coordination
The PipelineActor SHALL watch the egress actor. If the egress actor terminates (`Terminated` message), the pipeline actor SHALL stop its current graph and re-request a new SinkRef (triggering rematerialization once the egress is back).

#### Scenario: Egress restart triggers pipeline rematerialization
- **WHEN** the egress actor restarts and the pipeline receives `Terminated`
- **THEN** the pipeline actor stops its current graph, re-sends `RequestEgressSink`, stashes commands, and rematerializes when the new SinkRef arrives
