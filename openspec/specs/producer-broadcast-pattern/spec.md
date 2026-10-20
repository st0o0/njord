## Purpose

Defines the producer broadcast pattern: the stable, stateful actor of a seam owns the hubs (the pipeline's MergeHub input and BroadcastHub output, the read model's secondary hub), producers attach via SinkRef and consumers via SourceRef directly, and no central hub actor routes data.
## Requirements
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
No actor SHALL exist solely to merge inputs from multiple producers and broadcast the merged result to consumers. There SHALL be no MergeHub-plus-BroadcastHub intermediary such as the former `EgressActor`. `ForecastPipeline` owning a MergeHub input and a BroadcastHub output is not such an intermediary: it analyzes every element between the two hubs.

#### Scenario: System starts without hub actors
- **WHEN** the actor system starts with MQTT enabled
- **THEN** no actor is registered whose sole purpose is merging and broadcasting events from multiple producers

#### Scenario: Data flows from producer to consumer without intermediary
- **WHEN** `MqttStatePublisher` needs pipeline events
- **THEN** it subscribes directly to `ForecastPipeline`'s BroadcastHub and no intermediate hub actor relays the events

