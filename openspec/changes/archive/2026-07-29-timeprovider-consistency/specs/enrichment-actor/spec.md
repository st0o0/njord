## MODIFIED Requirements

### Requirement: The EnrichmentActor requests a SourceRef from the PipelineActor
The `EnrichmentActor` SHALL send a `RequestPipelineSource` message to the `PipelineActor` on startup. Upon receiving a `PipelineSourceResponse`, it SHALL transition from `WaitingForSourceRef` to its operational state. Messages received before the SourceRef arrives SHALL be stashed.

The actor SHALL resolve peer actor references (PipelineActor, EgressActor) asynchronously via `GetActorAsync` with `PipeTo` instead of synchronous `GetActor` in PreStart. This eliminates startup-order sensitivity.

#### Scenario: SourceRef received transitions to operational
- **WHEN** the EnrichmentActor starts and receives a `PipelineSourceResponse`
- **THEN** it transitions to its operational state and unstashes pending messages

#### Scenario: Messages are stashed before SourceRef
- **WHEN** the EnrichmentActor receives messages before the `PipelineSourceResponse`
- **THEN** the messages are stashed and replayed after the transition

#### Scenario: PipelineActor restart triggers re-request
- **WHEN** the EnrichmentActor receives a `Terminated` message for the PipelineActor
- **THEN** it sends a new `RequestPipelineSource` to the restarted PipelineActor

#### Scenario: Peer actors resolved asynchronously
- **WHEN** the EnrichmentActor starts
- **THEN** it resolves PipelineActor and EgressActor via GetActorAsync, not sync GetActor
