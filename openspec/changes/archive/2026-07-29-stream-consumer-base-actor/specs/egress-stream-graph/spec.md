# egress-stream-graph Delta Specification

## MODIFIED Requirements

### Requirement: ModelStateActor consumes FetchOutcome from Pipeline SourceRef
The `ModelStateActor` SHALL inherit from `StreamConsumerActor`. It SHALL resolve `EgressActor` and `PipelineActor` via `GetActorAsync` in its `ResolveDependencies()` override. In its `*Resolved` handlers it SHALL call `TrackDependency()` and check `IsDeadRef()`. It SHALL wire the base-provided `SharedKillSwitch.Flow<FetchOutcome>()` into its stream graph in `MaterializeGraph()`. It SHALL import `FetchOutcome` from `Njord.Domain.Weather`, not from `Njord.Ingest`. The Egress zone SHALL NOT reference the Ingest namespace. The MQTT-specific mapping is in `MqttEgressActor`. No stream stage in the MqttConnectionActor's graph SHALL directly consume `FetchOutcome` from the Pipeline BroadcastHub. The HandleTerminated behavior is fully managed by the `StreamConsumerActor` base.

#### Scenario: No direct pipeline-to-MQTT path
- **WHEN** the MqttConnectionActor's egress stream graph is materialized
- **THEN** no stream stage SHALL directly consume `FetchOutcome` from the Pipeline BroadcastHub — that responsibility belongs to `ModelStateActor`

#### Scenario: FetchOutcome.Success produces PerModelUpdate
- **WHEN** a FetchOutcome.Success arrives via the pipeline SourceRef
- **THEN** the ModelStateActor emits an EgressEvent.PerModelUpdate downstream

#### Scenario: No Ingest namespace import in Egress
- **WHEN** the codebase is compiled
- **THEN** no file under Njord.Egress contains `using Njord.Ingest`
