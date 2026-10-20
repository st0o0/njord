## ADDED Requirements

### Requirement: GrpcConsumer processes pipeline events on the actor thread
Pipeline events SHALL flow through the actor mailbox (Sink.ActorRef) and be processed by Receive handlers, not by stream-thread callbacks updating volatile state.

#### Scenario: Pipeline event updates actor state
- **WHEN** the pipeline emits a PipelineEvent
- **THEN** the event SHALL be delivered to GrpcConsumer's mailbox and processed by a Receive<PipelineEvent> handler that updates immutable actor state

### Requirement: GrpcConsumer uses Source.ActorRef for its BroadcastHub
The internal BroadcastHub for gRPC client fan-out SHALL be fed by Source.ActorRef (actor pushes via Tell), not by a MergeHub.

#### Scenario: Single producer feeds BroadcastHub
- **WHEN** GrpcConsumer receives a PipelineEvent via its mailbox
- **THEN** the actor SHALL update its state AND forward the event to the BroadcastHub via _hubInput.Tell(evt)

### Requirement: GrpcConsumer lives in Njord.Grpc
GrpcConsumer SHALL be declared in Njord.Grpc, not in Njord.Pipeline, because it is a gRPC adapter.

#### Scenario: Project reference direction
- **WHEN** checking assembly references
- **THEN** Njord.Grpc SHALL contain GrpcConsumer and its registration

### Requirement: Point queries read actor state
QueryForecast and QueryLatestAnalysis SHALL read from regular actor fields, not from volatile or external state.

#### Scenario: Concurrent query and event
- **WHEN** a QueryForecast arrives while a PipelineEvent is being processed
- **THEN** the query SHALL be processed after the event (actor mailbox serialization), reading consistent state
