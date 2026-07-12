## MODIFIED Requirements

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
