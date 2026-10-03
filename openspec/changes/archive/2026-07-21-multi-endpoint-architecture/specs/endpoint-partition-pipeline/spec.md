## ADDED Requirements

### Requirement: Pipeline builds a partition graph from registered modules
The `PipelineActor` SHALL build a `Partition<WeightedTarget>` stage with one outlet per registered `IEndpointModule`. The partition function SHALL route by `target.EndpointType.Index`. Each outlet SHALL be connected to the corresponding module's `BuildSubGraph()` flow. All module flows SHALL merge into a shared `MergeHub<EgressEvent>` that feeds the `EgressActor`.

#### Scenario: Two modules produce a 2-outlet partition
- **WHEN** `WeatherModule` and `AirQualityModule` are registered
- **THEN** the partition SHALL have 2 outlets, outlet 0 wired to `WeatherModule.BuildSubGraph()` and outlet 1 to `AirQualityModule.BuildSubGraph()`

#### Scenario: Single module degenerates to pass-through
- **WHEN** only `WeatherModule` is registered
- **THEN** the partition SHALL have 1 outlet and the pipeline SHALL behave identically to the pre-refactor single-stream pipeline

#### Scenario: Module sub-graph failure does not kill other outlets
- **WHEN** the AirQuality sub-graph throws an unhandled exception
- **THEN** the Weather sub-graph SHALL continue processing; the partition outlet for AQ applies its own supervision

### Requirement: BudgetThrottleStage precedes the partition
The `BudgetThrottleStage` SHALL remain a single shared stage positioned between the `MergeHub<WeightedTarget>` source and the `Partition<WeightedTarget>` stage. It SHALL operate on the abstract `Weight` property of `WeightedTarget` without knowledge of the concrete subtype.

#### Scenario: Budget stage throttles heterogeneous targets
- **WHEN** a `WeatherTarget(weight=3)` and an `AirQualityTarget(weight=1)` arrive
- **THEN** the budget stage SHALL consume 3 + 1 = 4 tokens from the shared bucket

#### Scenario: Budget stage does not inspect endpoint-specific fields
- **WHEN** a `WeatherTarget` passes through the budget stage
- **THEN** the stage SHALL access only `Weight` from the `WeightedTarget` base — never cast to `WeatherTarget`

### Requirement: EgressEvent merge collects from all module sub-graphs
All module sub-graphs SHALL emit `EgressEvent` instances that merge into a single `MergeHub<EgressEvent>` sink. The `EgressActor` SHALL consume this merged stream identically to how it consumed the single-stream pipeline before.

#### Scenario: Egress receives events from multiple endpoints
- **WHEN** Weather emits a `ModelUpdate` event and AirQuality emits an `EnrichmentUpdate` event
- **THEN** both SHALL arrive at the `EgressActor` via the merged `MergeHub<EgressEvent>`

#### Scenario: Egress ordering is non-deterministic across endpoints
- **WHEN** Weather and AirQuality emit events concurrently
- **THEN** the `EgressActor` SHALL process them in arrival order without assuming endpoint ordering
