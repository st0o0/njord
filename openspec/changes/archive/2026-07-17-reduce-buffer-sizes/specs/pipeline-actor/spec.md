## MODIFIED Requirements

### Requirement: PipelineActor materializes a BroadcastHub for FetchOutcome distribution
The `PipelineActor` SHALL materialize a `BroadcastHub.Sink<FetchOutcome>` with a buffer size of 16 to distribute fetch results to all consumers (ModelStateActor, EnrichmentActor).

#### Scenario: BroadcastHub buffer size is 16
- **WHEN** the PipelineActor materializes its stream graph
- **THEN** the BroadcastHub SHALL use a buffer size of 16
