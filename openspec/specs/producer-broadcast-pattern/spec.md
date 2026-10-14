## Purpose

Defines the producer broadcast pattern: each producer actor owns a BroadcastHub, consumers subscribe to producers directly via SourceRefs, and no central hub actor routes data.
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
No actor SHALL exist solely to merge inputs from multiple producers and broadcast the merged result to consumers. There SHALL be no MergeHub-plus-BroadcastHub intermediary such as the former `EgressActor`.

#### Scenario: System starts without hub actors
- **WHEN** the actor system starts with MQTT enabled
- **THEN** no actor is registered whose sole purpose is merging and broadcasting events from multiple producers

#### Scenario: Data flows from producer to consumer without intermediary
- **WHEN** `MqttStatePublisher` needs pipeline events
- **THEN** it subscribes directly to `ForecastPipeline`'s BroadcastHub and no intermediate hub actor relays the events

### Requirement: Producer actors own a BroadcastHub for their output
Each producing actor SHALL materialize a BroadcastHub for its output type: `OpenMeteoGateway` broadcasts `Forecast`, `ForecastPipeline` broadcasts `PipelineEvent`, and `ForecastReadModel` re-broadcasts the `PipelineEvent`s it consumed through a secondary hub for streaming gRPC calls. The BroadcastHub is the sole distribution mechanism for that producer's data — no separate hub actor aggregates or redistributes it.

#### Scenario: Gateway broadcasts forecasts
- **WHEN** `OpenMeteoGateway` completes a successful fetch
- **THEN** the `Forecast` is available to every consumer holding a SourceRef from the gateway's BroadcastHub

#### Scenario: ForecastPipeline broadcasts pipeline events
- **WHEN** `ForecastPipeline` processes a forecast
- **THEN** the emitted `PipelineEvent`s are available to every consumer holding a SourceRef from its BroadcastHub

#### Scenario: ForecastReadModel re-broadcasts for streaming clients
- **WHEN** `ForecastReadModel` receives a `PipelineEvent`
- **THEN** it is available to every streaming gRPC call holding a SourceRef from the read model's secondary hub

### Requirement: Consumers request SourceRefs directly from producers
A consumer actor SHALL request a SourceRef from the specific producer actor whose data it needs (`RequestForecastStream`, `RequestPipelineOutput`, `RequestReadModelStream`). The producer SHALL respond with a SourceRef connected to its BroadcastHub. No intermediary actor SHALL be involved in the subscription.

#### Scenario: Consumer requests source from producer
- **WHEN** a consumer actor sends a source request message to a producer actor
- **THEN** the producer SHALL respond with a SourceRef connected to its BroadcastHub
- **AND** the consumer SHALL materialize a stream from that SourceRef to process the producer's output

#### Scenario: Each consumer subscribes to exactly the producer it needs
- **WHEN** `ForecastPipeline`, `MqttStatePublisher` and `ForecastReadModel` start
- **THEN** `ForecastPipeline` subscribes to the gateway, and `MqttStatePublisher` and `ForecastReadModel` each subscribe to `ForecastPipeline`

