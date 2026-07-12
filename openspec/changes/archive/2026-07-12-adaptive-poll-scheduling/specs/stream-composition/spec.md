## MODIFIED Requirements

### Requirement: The pipeline is a single materialized graph
The pipeline SHALL be materialized exactly once by the `PipelineActor` using `Context.Materializer()` (actor-bound lifecycle). No stage within the graph SHALL materialize a sub-stream via `RunWith` or equivalent. The graph SHALL remain materialized for the lifetime of the actor incarnation. No `IHostedService` or manual `KillSwitch` SHALL be used.

#### Scenario: No per-cycle materialization
- **WHEN** 10 targets are processed
- **THEN** zero additional stream materializations occur beyond the initial actor-bound materialization

#### Scenario: Actor stop terminates graph
- **WHEN** the PipelineActor stops
- **THEN** the pipeline graph completes without requiring explicit KillSwitch shutdown

### Requirement: Source.Queue accepts targets from the SchedulerActor
The pipeline entry point SHALL be a `Source.Queue<WeightedTarget>` that the SchedulerActor feeds via `OfferAsync`. The MergeHub is removed. The materialized queue handle SHALL be provided to the SchedulerActor on request.

#### Scenario: SchedulerActor feeds targets into the queue
- **WHEN** the SchedulerActor offers a `WeightedTarget` via the queue handle
- **THEN** the target enters the pipeline for throttling, fetching, and publishing

#### Scenario: Queue provides backpressure when pipeline is saturated
- **WHEN** the throttle is at capacity and a new target is offered
- **THEN** the `OfferAsync` completes only when the queue has capacity

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

## REMOVED Requirements

### Requirement: MergeHub accepts commands from multiple sources
**Reason**: Replaced by `Source.Queue<WeightedTarget>`. The MergeHub was needed when multiple sources (tick, manual commands) attached independently. Now all targets flow through the SchedulerActor into a single queue.
**Migration**: Replace `MergeHub.Source<PipelineCommand>` with `Source.Queue<WeightedTarget>`. Manual commands go to the SchedulerActor, which offers targets into the same queue.
