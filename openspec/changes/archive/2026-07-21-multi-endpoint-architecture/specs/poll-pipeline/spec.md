## MODIFIED Requirements

### Requirement: Targets arrive via MergeHub from producers connected by SinkRef
The pipeline SHALL receive `WeightedTarget` elements (abstract base type, concrete subclasses per endpoint) via a `MergeHub.Source<WeightedTarget>` that producers connect to through a `SinkRef<WeightedTarget>` obtained from the PipelineActor. Each `WeightedTarget` SHALL carry a `CycleId` assigned by the producing actor and an `EndpointType` identifying which endpoint module produced it. There is no tick source — all poll timing is owned by the SchedulerActor.

#### Scenario: Targets arrive from the SchedulerActor via SinkRef
- **WHEN** the SchedulerActor's timer fires for (lucerne, icon_d2)
- **THEN** a `WeatherTarget` for that pair is offered into the SchedulerActor's local queue, which drains through the SinkRef into the pipeline's MergeHub

#### Scenario: Heterogeneous targets share the same MergeHub
- **WHEN** the SchedulerActor fires timers for Weather and AirQuality
- **THEN** both `WeatherTarget` and `AirQualityTarget` instances arrive in the same `MergeHub<WeightedTarget>`

#### Scenario: No raw queue handle crosses actor boundaries
- **WHEN** the SchedulerActor connects to the pipeline
- **THEN** it uses a `SinkRef<WeightedTarget>`, not a raw `ISourceQueueWithComplete`

#### Scenario: CycleId is assigned before entering the pipeline
- **WHEN** the SchedulerActor creates a `WeightedTarget`
- **THEN** the target carries a `CycleId` with the timestamp from when the scheduler decided to poll

### Requirement: Outbound requests respect the per-minute budget
The pipeline SHALL throttle outbound fetch requests using a `BudgetThrottleStage` that derives its rate from `IBudgetProvider.GetCurrentRate()`. The stage SHALL implement weighted token-bucket throttling based on the effective budget (default: 80% of `RequestsPerMinute`). The stage SHALL operate on the abstract `Weight` property of `WeightedTarget` without knowledge of the concrete subtype. The stage SHALL adapt to budget changes at runtime without re-materializing the graph. The `BudgetThrottleStage` SHALL be positioned before the `Partition<WeightedTarget>` stage so that all endpoint types share a single budget pool.

#### Scenario: Throttle rate derived from budget
- **WHEN** the effective budget is 600 req/min (free tier)
- **THEN** the stage SHALL throttle at 480 weighted cost-units per minute (80% politeness margin)

#### Scenario: Budget override changes throttle rate at runtime
- **WHEN** the user sets a `BudgetOverride` of 60 req/min via gRPC
- **THEN** the stage SHALL adapt to 48 cost-units per minute within 5 seconds

#### Scenario: Budget stage throttles heterogeneous targets by weight
- **WHEN** a `WeatherTarget(weight=3)` and an `AirQualityTarget(weight=1)` arrive sequentially
- **THEN** the budget stage SHALL consume 4 tokens total from the shared bucket

### Requirement: Fetch outcomes fan out via per-module BroadcastHub to egress
Each endpoint module's sub-graph SHALL handle its own fetch outcomes internally. Module sub-graphs SHALL emit `EgressEvent` instances that merge into a shared `MergeHub<EgressEvent>` sink consumed by the `EgressActor`. Egress unavailability (broker down) MUST NOT fail or stall any fetch sub-graph — the MergeHub decouples the paths.

#### Scenario: Egress receives events from multiple endpoint modules
- **WHEN** the Weather module emits a `ModelUpdate` and the AQ module emits an `EnrichmentUpdate`
- **THEN** both SHALL arrive at the `EgressActor` via the merged `MergeHub<EgressEvent>`

#### Scenario: Broker outage does not stall fetching
- **WHEN** the MQTT broker is unreachable
- **THEN** all endpoint module sub-graphs continue fetching; the egress consumer handles the failure independently

#### Scenario: Feedback hash loop remains per-module
- **WHEN** a Weather fetch succeeds
- **THEN** the Weather module's sub-graph computes a `HashResult` and sends it to the SchedulerActor — this logic is internal to the module, not orchestrated by PipelineActor
