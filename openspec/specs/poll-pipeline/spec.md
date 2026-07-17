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
The pipeline SHALL throttle outbound fetch requests using a `BudgetThrottleStage` that derives its rate from `IBudgetProvider.GetCurrentRate()`. The stage SHALL implement weighted token-bucket throttling based on the effective budget (default: 80% of `RequestsPerMinute`). The stage SHALL adapt to budget changes at runtime without re-materializing the graph. The fetch call SHALL use `SelectAsyncUnordered(2)` to limit concurrent connections to Open-Meteo to 2.

#### Scenario: Throttle rate derived from budget
- **WHEN** the effective budget is 600 req/min (free tier)
- **THEN** the stage SHALL throttle at 480 weighted cost-units per minute (80% politeness margin)

#### Scenario: Budget override changes throttle rate at runtime
- **WHEN** the user sets a `BudgetOverride` of 60 req/min via gRPC
- **THEN** the stage SHALL adapt to 48 cost-units per minute within 5 seconds

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
