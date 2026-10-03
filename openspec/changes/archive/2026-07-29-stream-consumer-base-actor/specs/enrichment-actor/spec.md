# enrichment-actor Delta Specification

## MODIFIED Requirements

### Requirement: The EnrichmentActor requests a SourceRef from the PipelineActor
The `EnrichmentActor` SHALL inherit from `StreamConsumerActor`. It SHALL resolve `PipelineActor` and `EgressActor` via `GetActorAsync` in its `ResolveDependencies()` override. In its `*Resolved` handlers it SHALL call `TrackDependency()` and check `IsDeadRef()`. It SHALL wire the base-provided `SharedKillSwitch.Flow<FetchOutcome>()` into its stream graph in `MaterializeGraph()`. Messages received before all refs arrive SHALL be stashed by the base. The HandleTerminated behavior is fully managed by the `StreamConsumerActor` base: KillSwitch shutdown, dead-ref detection with exponential backoff retry, stale-response gating.

#### Scenario: SourceRef received transitions to operational
- **WHEN** the EnrichmentActor starts and receives both PipelineSourceResponse and EgressSinkResponse
- **THEN** it transitions to its operational state and unstashes pending messages

#### Scenario: Messages are stashed before SourceRef
- **WHEN** the EnrichmentActor receives messages before all refs arrive
- **THEN** the messages are stashed and replayed after the transition

#### Scenario: PipelineActor restart triggers re-request
- **WHEN** the EnrichmentActor receives a `Terminated` message for a tracked dependency
- **THEN** it shuts down the KillSwitch and re-requests with backoff retry

#### Scenario: Peer actors resolved asynchronously
- **WHEN** the EnrichmentActor starts
- **THEN** it resolves PipelineActor and EgressActor via GetActorAsync, not sync GetActor
