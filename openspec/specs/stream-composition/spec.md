# stream-composition Specification

## Purpose

Defines the structural requirements for the Akka.Streams pipeline graph: single materialization, Source.Queue entry point, weighted throttle, direct publish without aggregation, hash and schedule feedback stages, independently testable stages, stream supervision, graceful shutdown, and observability.

## Requirements

### Requirement: The pipeline is a single materialized graph
The pipeline SHALL be materialized exactly once by the `PipelineActor` using `Context.Materializer()` (actor-bound lifecycle). No stage within the graph SHALL materialize a sub-stream via `RunWith` or equivalent. The graph SHALL remain materialized for the lifetime of the actor incarnation. No `IHostedService` or manual `KillSwitch` SHALL be used.

#### Scenario: No per-cycle materialization
- **WHEN** 10 targets are processed
- **THEN** zero additional stream materializations occur beyond the initial actor-bound materialization

#### Scenario: Actor stop terminates graph
- **WHEN** the PipelineActor stops
- **THEN** the pipeline graph completes without requiring explicit KillSwitch shutdown

### Requirement: Source.Queue accepts targets from the SchedulerActor
The pipeline entry point SHALL be a `Source.Queue<WeightedTarget>` that the SchedulerActor feeds via `OfferAsync`. The materialized queue handle SHALL be provided to the SchedulerActor on request.

#### Scenario: SchedulerActor feeds targets into the queue
- **WHEN** the SchedulerActor offers a `WeightedTarget` via the queue handle
- **THEN** the target enters the pipeline for throttling, fetching, and publishing

#### Scenario: Queue provides backpressure when pipeline is saturated
- **WHEN** the throttle is at capacity and a new target is offered
- **THEN** the `OfferAsync` completes only when the queue has capacity

### Requirement: Weighted throttle enforces the global API budget
The pipeline SHALL throttle fetch requests using a cost-weighted throttle set to 80% of the Open-Meteo per-minute rate limit. Each element's cost SHALL be its pre-calculated API weight. The throttle SHALL use shaping mode (delay, not drop).

#### Scenario: Weight-1 requests within budget pass immediately
- **WHEN** 8 weight-1 targets enter the throttle with budget ceiling 480/min
- **THEN** all 8 pass through without delay

#### Scenario: Burst beyond budget is shaped
- **WHEN** 500 weight-1 targets enter the throttle with budget ceiling 480/min
- **THEN** the first 480 pass within the first minute; the remaining 20 are delayed into the next minute window

### Requirement: Fetch outcomes publish directly without aggregation
Each successful `FetchOutcome` SHALL be mapped to `MqttMessage`(s) and delivered to the egress. The pipeline SHALL NOT call `IMqttTransport.PublishAsync` directly but use the egress SinkRef or a side-effect stage. Failed outcomes SHALL NOT produce a message; they are logged and filtered.

#### Scenario: Successful fetch produces MqttMessages
- **WHEN** a fetch for (lucerne, icon_d2) succeeds with 3 changed horizons
- **THEN** 3 MqttMessages are emitted toward the egress

#### Scenario: Failed fetch does not produce MqttMessage
- **WHEN** a fetch fails
- **THEN** no MqttMessage is emitted for that target

### Requirement: Hash and schedule feedback stages follow publish
After the MQTT publish stage, the pipeline SHALL include a synchronous `Select` stage that computes a data hash over the `FetchOutcome`, followed by the built-in Akka.Streams `Ask<Ack>` flow that sends the `HashResult` to the SchedulerActor and waits for acknowledgement. The terminal sink is `Sink.Ignore`.

#### Scenario: Hash is computed synchronously
- **WHEN** a `FetchOutcome.Success` passes through the hash stage
- **THEN** a `HashResult` is produced without async overhead

#### Scenario: Ask flow sends hash to SchedulerActor
- **WHEN** a `HashResult` is produced
- **THEN** it is sent to the SchedulerActor via Ask and the stream waits for `Ack`

### Requirement: Pipeline stages are independently testable flows
Each processing stage (throttle+fetch, publish, hash, ask) SHALL be implemented as a standalone `Flow<TIn, TOut>` or `Sink<TIn>` factory method. Tests SHALL be able to wire a `TestSource` to any stage without materializing the full pipeline.

#### Scenario: Hash stage is testable in isolation
- **WHEN** a test wires `TestSource<FetchOutcome> -> HashStage -> TestSink`
- **THEN** the hash computation can be verified without an MQTT connection or SchedulerActor

#### Scenario: Fetch stage is testable with a mock client
- **WHEN** a test wires `TestSource<WeightedTarget> -> FetchStage(mockClient) -> TestSink`
- **THEN** fetch behavior (parallelism, error handling) can be verified in isolation

### Requirement: Stream supervision resumes on transient fetch failures
The fetch stage SHALL use a supervision decider that resumes processing on transient failures (HTTP timeout, rate-limit 429, transport errors). A single failed fetch SHALL NOT restart or terminate the graph. Persistent failures (repeated errors for the same target) SHALL be logged but SHALL NOT stall the pipeline.

#### Scenario: HTTP timeout does not kill the stream
- **WHEN** a single fetch times out
- **THEN** the stream continues processing subsequent targets; the timed-out target emits a `FetchOutcome.Failure`

#### Scenario: Repeated failures for one model do not stall others
- **WHEN** model "icon_d2" fails 5 times consecutively
- **THEN** other models continue to be fetched and published on schedule

### Requirement: Clean shutdown via actor lifecycle
The pipeline SHALL terminate cleanly when the PipelineActor stops. In-flight fetches SHALL complete (or timeout) before the graph finalizes. No explicit `KillSwitch` management is required — `Context.Materializer()` binds the graph to the actor.

#### Scenario: Graceful shutdown completes in-flight work
- **WHEN** the actor is stopped while 3 fetches are in progress
- **THEN** those fetches complete (or timeout) and the graph terminates

### Requirement: Observability via structured logging per outcome
The pipeline SHALL emit one structured log entry per fetch outcome (success or failure) containing location, model, duration, and result status. A periodic summary (targets fetched since last summary, success rate) SHALL be emittable via a side-channel (`WireTap` or equivalent) without blocking the main flow.

#### Scenario: Each fetch logs its outcome
- **WHEN** a fetch for (lucerne, icon_d2) completes in 1200ms
- **THEN** a structured log entry is emitted with location="lucerne", model="icon_d2", duration=1200ms, status="success"
