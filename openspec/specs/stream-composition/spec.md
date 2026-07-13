# stream-composition Specification

## Purpose

Defines the structural requirements for the Akka.Streams pipeline graph: single materialization, MergeHub entry point with SinkRef-based producer access, weighted throttle, BroadcastHub fan-out for egress and feedback consumers, independently testable stages, stream supervision, graceful shutdown, and observability.

## Requirements

### Requirement: The pipeline is a single materialized graph
The pipeline SHALL be materialized exactly once by the `PipelineActor` using `Context.Materializer()` (actor-bound lifecycle). The PipelineActor SHALL materialize the pipeline graph independently -- it SHALL NOT wait for the EgressActor or SchedulerActor before materializing. The BroadcastHub buffers until consumers connect. No stage within the graph SHALL materialize a sub-stream via `RunWith` or equivalent, except for the BroadcastHub consumer graphs and the SinkRef/SourceRef materializations which are part of the actor's setup. The graph SHALL remain materialized for the lifetime of the actor incarnation. No `IHostedService` or manual `KillSwitch` SHALL be used.

#### Scenario: No per-cycle materialization
- **WHEN** 10 targets are processed
- **THEN** zero additional stream materializations occur beyond the initial actor-bound materialization and consumer setup

#### Scenario: Actor stop terminates graph
- **WHEN** the PipelineActor stops
- **THEN** the pipeline graph and all BroadcastHub consumers complete without requiring explicit KillSwitch shutdown

#### Scenario: Pipeline materializes without waiting for consumers
- **WHEN** the PipelineActor starts
- **THEN** the pipeline graph (MergeHub -> flow -> BroadcastHub) is materialized immediately, without waiting for EgressActor or SchedulerActor readiness

### Requirement: MergeHub accepts targets from producers via SinkRef
The pipeline entry point SHALL be a `MergeHub.Source<WeightedTarget>` that producers connect to via a `SinkRef<WeightedTarget>` obtained from the PipelineActor. The PipelineActor SHALL vend the SinkRef on request. Producers SHALL NOT receive a raw queue handle.

#### Scenario: SchedulerActor connects via SinkRef
- **WHEN** the SchedulerActor sends a `RequestPipelineSink` message
- **THEN** the PipelineActor responds with a `PipelineSinkResponse` containing a `SinkRef<WeightedTarget>` connected to the MergeHub

#### Scenario: No raw queue handle crosses actor boundaries
- **WHEN** the pipeline is materialized
- **THEN** no `ISourceQueueWithComplete` is exposed to any external actor

#### Scenario: MergeHub provides per-producer buffering
- **WHEN** a producer sends elements faster than the throttle drains them
- **THEN** backpressure propagates to the producer through the SinkRef

### Requirement: Weighted throttle enforces the global API budget
The pipeline SHALL throttle fetch requests using a cost-weighted throttle set to 80% of the Open-Meteo per-minute rate limit. Each element's cost SHALL be its pre-calculated API weight. The throttle SHALL use shaping mode (delay, not drop).

#### Scenario: Weight-1 requests within budget pass immediately
- **WHEN** 8 weight-1 targets enter the throttle with budget ceiling 480/min
- **THEN** all 8 pass through without delay

#### Scenario: Burst beyond budget is shaped
- **WHEN** 500 weight-1 targets enter the throttle with budget ceiling 480/min
- **THEN** the first 480 pass within the first minute; the remaining 20 are delayed into the next minute window

### Requirement: BroadcastHub fans out fetch outcomes to multiple consumers
After the filter stage (successful fetch outcomes only), the pipeline SHALL terminate with a `BroadcastHub.Sink<FetchOutcome.Success>`. The BroadcastHub SHALL provide a `Source` that multiple consumers can materialize independently. Each consumer SHALL receive every element and run with independent backpressure.

#### Scenario: Two consumers receive the same element
- **WHEN** a `FetchOutcome.Success` enters the BroadcastHub
- **THEN** both the egress consumer and the feedback consumer receive it

#### Scenario: Slow consumer does not lose elements
- **WHEN** the feedback consumer is slower than the egress consumer
- **THEN** the BroadcastHub buffers elements for the slow consumer; no elements are dropped

### Requirement: Fetch outcomes publish directly without aggregation
Each successful `FetchOutcome` SHALL be delivered to the BroadcastHub. The egress consumer (not the pipeline itself) SHALL map outcomes to `MqttMessage`(s) and deliver them to the MQTT transport. Failed outcomes SHALL NOT reach the BroadcastHub; they are logged and filtered in the pipeline flow.

#### Scenario: Successful fetch reaches BroadcastHub
- **WHEN** a fetch for (lucerne, icon_d2) succeeds
- **THEN** the `FetchOutcome.Success` is emitted into the BroadcastHub

#### Scenario: Failed fetch does not reach BroadcastHub
- **WHEN** a fetch fails
- **THEN** no element is emitted into the BroadcastHub for that target

### Requirement: Hash and schedule feedback is a BroadcastHub consumer
The PipelineActor SHALL materialize a local BroadcastHub consumer that computes a data hash over each `FetchOutcome.Success` and sends it to the SchedulerActor via the built-in Akka.Streams `Ask<Ack>` flow. The terminal sink is `Sink.Ignore`. This consumer SHALL be materialized by the PipelineActor, not the SchedulerActor.

#### Scenario: Hash is computed and sent via Ask
- **WHEN** a `FetchOutcome.Success` is received by the feedback consumer
- **THEN** a `HashResult` is computed and sent to the SchedulerActor via Ask; the consumer waits for `Ack`

#### Scenario: Ask backpressure propagates through BroadcastHub
- **WHEN** the SchedulerActor is slow to respond to a HashResult Ask
- **THEN** the feedback consumer slows down, and the BroadcastHub propagates backpressure to the pipeline flow

### Requirement: Pipeline stages are independently testable flows
Each processing stage (throttle+fetch, filter, hash, ask) SHALL be implemented as a standalone `Flow<TIn, TOut>` or `Sink<TIn>` factory method. Tests SHALL be able to wire a `TestSource` to any stage without materializing the full pipeline.

#### Scenario: Fetch stage is testable with a mock client
- **WHEN** a test wires `TestSource<WeightedTarget> -> FetchStage(mockClient) -> TestSink`
- **THEN** fetch behavior (parallelism, error handling) can be verified in isolation

### Requirement: Stream supervision resumes on transient fetch failures
The fetch stage SHALL use a supervision decider that resumes processing on transient failures (HTTP timeout, rate-limit 429, transport errors). A single failed fetch SHALL NOT restart or terminate the graph. Persistent failures (repeated errors for the same target) SHALL be logged but SHALL NOT stall the pipeline.

#### Scenario: HTTP timeout does not kill the stream
- **WHEN** a single fetch times out
- **THEN** the stream continues processing subsequent targets; the timed-out target emits a `FetchOutcome.Failure`

### Requirement: Clean shutdown via actor lifecycle
The pipeline SHALL terminate cleanly when the PipelineActor stops. In-flight fetches SHALL complete (or timeout) before the graph finalizes. No explicit `KillSwitch` management is required — `Context.Materializer()` binds the graph to the actor.

#### Scenario: Graceful shutdown completes in-flight work
- **WHEN** the actor is stopped while 3 fetches are in progress
- **THEN** those fetches complete (or timeout) and the graph terminates

### Requirement: Observability via structured logging per outcome
The pipeline SHALL emit one structured log entry per fetch outcome (success or failure) containing location, model, duration, and result status.

#### Scenario: Each fetch logs its outcome
- **WHEN** a fetch for (lucerne, icon_d2) completes in 1200ms
- **THEN** a structured log entry is emitted with location="lucerne", model="icon_d2", duration=1200ms, status="success"

### Requirement: The EnrichmentActor is a BroadcastHub consumer via SourceRef
The `EnrichmentActor` SHALL request a `SourceRef<FetchOutcome>` from the `PipelineActor` using the existing `RequestPipelineSource` / `PipelineSourceResponse` protocol. This makes the EnrichmentActor a third consumer of the pipeline's BroadcastHub, alongside the EgressActor's consumer graph and the PipelineActor's local feedback consumer. The PipelineActor SHALL NOT require any changes to support this — the existing SourceRef vending mechanism supports multiple consumers.

#### Scenario: Three consumers receive the same fetch outcome
- **WHEN** a `FetchOutcome.Success` enters the BroadcastHub
- **THEN** the egress consumer, the feedback consumer, and the EnrichmentActor's consumer all receive it

#### Scenario: EnrichmentActor connects independently
- **WHEN** the EnrichmentActor starts and requests a SourceRef
- **THEN** the PipelineActor vends a SourceRef without any protocol changes

#### Scenario: EnrichmentActor failure does not affect other consumers
- **WHEN** the EnrichmentActor's consumer stream fails
- **THEN** the egress consumer and feedback consumer continue operating; the EnrichmentActor restarts and re-requests a SourceRef
