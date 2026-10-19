## ADDED Requirements

### Requirement: Stream supervision resumes all exceptions
The stream supervision decider SHALL resume on ALL exceptions, logging non-transient exceptions as Error. No exception SHALL stop or restart a stream stage.

#### Scenario: Analysis bug in StatefulSelectMany
- **WHEN** ConsensusBuilder or an analyzer throws an exception during processing
- **THEN** the exception SHALL be logged as Error, the failing element SHALL be dropped, and the stream SHALL continue with its accumulated state preserved

#### Scenario: Transient exception in stream
- **WHEN** a transient exception (timeout, HTTP error, cancellation) occurs in a stream stage
- **THEN** the exception SHALL be logged as Warning and the element SHALL be dropped (unchanged from current behavior)

### Requirement: Pipeline hubs are materialized once and never die
The MergeHub and BroadcastHub SHALL be PreMaterialized once in PreStart and SHALL survive analysis graph failures and re-materializations.

#### Scenario: Analysis graph ends
- **WHEN** the analysis graph completes or fails despite Resume-all
- **THEN** the pipeline SHALL re-materialize only the analysis graph, connecting to the same PreMaterialized hubs. Consumers and producers SHALL not be affected.

#### Scenario: Consumer SourceRef lifetime
- **WHEN** a consumer receives a SourceRef from RequestPipelineOutput
- **THEN** the SourceRef SHALL remain valid for the lifetime of the pipeline actor (i.e., until process shutdown)

### Requirement: Pipeline actor never restarts on analysis failure
The pipeline actor SHALL NOT be wrapped in a BackoffSupervisor. Analysis failures are handled by stream supervision (Resume) and graph re-materialization, not by actor restart.

#### Scenario: No BackoffSupervisor
- **WHEN** the pipeline actor is registered
- **THEN** it SHALL use plain Props, not BackoffOnFailure

### Requirement: StreamConsumerActor is eliminated
Consumers SHALL inherit ReceiveActor directly with one-shot attach logic. No base class for stream consumer re-attachment SHALL exist.

#### Scenario: GrpcConsumer attaches once
- **WHEN** GrpcConsumer starts
- **THEN** it SHALL resolve the pipeline, request a SourceRef, and materialize its graph once. No retry, no backoff, no re-attach.

#### Scenario: MqttStatePublisher attaches once
- **WHEN** MqttStatePublisher starts
- **THEN** it SHALL resolve the pipeline, request a SourceRef, and materialize its graph once. No retry, no backoff, no re-attach.
