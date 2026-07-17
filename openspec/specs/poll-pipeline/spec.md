# poll-pipeline Specification

## Purpose

Polling pipeline that receives individual fetch targets from producers via MergeHub (connected by SinkRef), throttles to the request budget, fans out successful outcomes via BroadcastHub to egress and feedback consumers, and logs per-outcome structured summaries.

## Requirements

### Requirement: Targets arrive via MergeHub from producers connected by SinkRef
The pipeline SHALL receive `WeightedTarget` elements via a `MergeHub.Source<WeightedTarget>` that producers connect to through a `SinkRef<WeightedTarget>` obtained from the PipelineActor. Each `WeightedTarget` SHALL carry a `CycleId` assigned by the producing actor. There is no tick source -- all poll timing is owned by the SchedulerActor.

#### Scenario: Targets arrive from the SchedulerActor via SinkRef
- **WHEN** the SchedulerActor's timer fires for (lucerne, icon_d2)
- **THEN** a `WeightedTarget` for that pair is offered into the SchedulerActor's local queue, which drains through the SinkRef into the pipeline's MergeHub

#### Scenario: No raw queue handle crosses actor boundaries
- **WHEN** the SchedulerActor connects to the pipeline
- **THEN** it uses a `SinkRef<WeightedTarget>`, not a raw `ISourceQueueWithComplete`

#### Scenario: CycleId is assigned before entering the pipeline
- **WHEN** the SchedulerActor creates a `WeightedTarget`
- **THEN** the target carries a `CycleId` with the timestamp from when the scheduler decided to poll

### Requirement: Outbound requests respect the per-minute budget
The pipeline SHALL throttle outbound fetch requests using a fixed politeness rate of **2 elements per second** with a maximum burst of 4, using `ThrottleMode.Shaping`. The throttle SHALL be element-count-based (not weighted). The fetch call SHALL use `SelectAsyncUnordered(2)` to limit concurrent connections to Open-Meteo to 2.

#### Scenario: Throttle rate is 2 per second
- **WHEN** 27 weight-1 targets enter the pipeline simultaneously at startup
- **THEN** the throttle shapes them to 2 per second (with an initial burst of up to 4), completing all 27 in approximately 14 seconds

#### Scenario: Throttle is independent of budget configuration
- **WHEN** the user sets a `BudgetOverride` of 60 req/min
- **THEN** the pipeline throttle rate remains at 2 req/sec (the budget override affects monthly/minute accounting, not the politeness throttle)

#### Scenario: Maximum 2 concurrent HTTP calls
- **WHEN** the throttle releases 2 targets within the same second
- **THEN** `SelectAsyncUnordered(2)` processes both concurrently but does not start a third until one completes

#### Scenario: Fetch is inlined in the pipeline graph
- **WHEN** the PipelineActor materializes the pipeline
- **THEN** the fetch logic is a direct `SelectAsyncUnordered` call to `IOpenMeteoClient.FetchAsync`, not a separate `FetchStage.Create()` flow

### Requirement: Fetch outcomes fan out via BroadcastHub to egress and feedback consumers
The pipeline SHALL broadcast each successful `FetchOutcome` via a `BroadcastHub`. The EgressActor SHALL consume the BroadcastHub via a `SourceRef` and map outcomes to `MqttMessage`(s) for MQTT publish. The PipelineActor SHALL materialize a local feedback consumer that computes a data hash and sends it to the SchedulerActor via Ask. Egress unavailability (broker down) MUST NOT fail or stall the fetch pipeline -- the BroadcastHub decouples the two paths.

#### Scenario: Egress receives outcome via BroadcastHub SourceRef
- **WHEN** a fetch for (lucerne, icon_d2) succeeds
- **THEN** the `FetchOutcome.Success` is broadcast via BroadcastHub and received by the EgressActor's consumer graph

#### Scenario: Feedback consumer computes hash and asks scheduler
- **WHEN** a fetch for (lucerne, icon_d2) succeeds
- **THEN** the feedback consumer computes a `HashResult` and sends it to the SchedulerActor via Ask

#### Scenario: Ask flow provides backpressure via BroadcastHub
- **WHEN** the SchedulerActor is processing a previous HashResult
- **THEN** the feedback consumer waits for Ack; BroadcastHub propagates this as backpressure to the pipeline

#### Scenario: Broker outage does not stall fetching
- **WHEN** the MQTT broker is unreachable
- **THEN** the pipeline continues fetching; the egress consumer handles the failure independently
