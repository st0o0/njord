## ADDED Requirements

### Requirement: Producer actors own their broadcast output

Each producing actor SHALL materialize a BroadcastHub for its output type during graph setup. PipelineActor SHALL broadcast `FetchOutcome`, ModelStateActor SHALL broadcast `EgressEvent`, and EnrichmentActor SHALL broadcast `EgressEvent`. The BroadcastHub is the sole distribution mechanism for that producer's data — no separate hub actor aggregates or redistributes it.

#### Scenario: PipelineActor broadcasts fetch results
- **WHEN** PipelineActor completes an HTTP fetch producing a FetchOutcome
- **THEN** the FetchOutcome SHALL be available to all consumers subscribed to PipelineActor's BroadcastHub

#### Scenario: ModelStateActor broadcasts egress events
- **WHEN** ModelStateActor processes a FetchOutcome into PerModelUpdate and CapabilityLearned events
- **THEN** those EgressEvents SHALL be available to all consumers subscribed to ModelStateActor's BroadcastHub

#### Scenario: EnrichmentActor broadcasts enrichment events
- **WHEN** EnrichmentActor computes consensus and enrichment results from a ModelSnapshot
- **THEN** the resulting EgressEvents SHALL be available to all consumers subscribed to EnrichmentActor's BroadcastHub

### Requirement: Consumers subscribe to producers directly

A consumer actor SHALL request a SourceRef from the specific producer actor whose data it needs. The producer SHALL respond with a SourceRef connected to its BroadcastHub. No intermediary actor SHALL be involved in the subscription.

#### Scenario: Consumer requests source from producer
- **WHEN** a consumer actor sends a source request message to a producer actor
- **THEN** the producer SHALL respond with a SourceRef connected to its BroadcastHub
- **AND** the consumer SHALL materialize a stream from that SourceRef to process the producer's output

#### Scenario: Consumer subscribes to multiple producers
- **WHEN** a consumer actor needs data from both ModelStateActor and EnrichmentActor (e.g. MqttStateActor)
- **THEN** the consumer SHALL request a SourceRef from each producer independently
- **AND** merge the resulting streams within its own graph

### Requirement: Multiple concurrent consumers per producer

A producer actor SHALL support multiple consumers holding active SourceRefs to its BroadcastHub simultaneously. Adding or removing a consumer SHALL NOT disrupt other active consumers.

#### Scenario: Second consumer subscribes while first is active
- **WHEN** a second consumer requests a SourceRef from a producer that already has one active consumer
- **THEN** the producer SHALL provide a new SourceRef
- **AND** both consumers SHALL receive all subsequent output independently

#### Scenario: Consumer disconnects without affecting others
- **WHEN** one consumer's SourceRef stream completes or fails
- **THEN** other consumers subscribed to the same producer SHALL continue receiving data uninterrupted

### Requirement: No central hub routing actor

No actor SHALL exist solely to merge inputs from multiple producers and broadcast the merged result to consumers. The EgressActor hub pattern — where producers push into a MergeHub and consumers pull from a BroadcastHub on a single intermediary actor — SHALL be eliminated.

#### Scenario: System starts without hub actors
- **WHEN** the actor system starts with MQTT enabled
- **THEN** no actor SHALL be registered whose sole purpose is merging and broadcasting EgressEvents from multiple sources

#### Scenario: Data flows from producer to consumer without intermediary
- **WHEN** MqttStateActor needs EgressEvents from ModelStateActor
- **THEN** MqttStateActor SHALL subscribe directly to ModelStateActor's BroadcastHub
- **AND** no intermediate hub actor SHALL relay the events
