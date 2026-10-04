## Requirements

### Requirement: No Akka.Streams types in shared messages

The Njord.Messages project SHALL NOT have a package reference to Akka.Streams. No record in Njord.Messages SHALL reference ISinkRef, ISourceRef, or any other Akka.Streams type. Records that currently contain these types SHALL be moved to actor-internal records in the feature library that owns them.

#### Scenario: Messages project has no Akka.Streams dependency
- **WHEN** the Njord.Messages.csproj is inspected
- **THEN** it SHALL NOT contain a PackageReference to Akka.Streams or any Akka.Streams.* package

#### Scenario: SinkRef request records are actor-internal
- **WHEN** an actor needs to request a SinkRef from another actor
- **THEN** the request and response records SHALL be defined as private or internal records within the requesting actor's feature library
- **AND** they SHALL NOT appear in the Njord.Messages project

### Requirement: Stream wiring records are actor-internal

Request/Response records for stream handshakes (requesting and providing SinkRefs or SourceRefs) SHALL be defined privately within the actor or feature library that uses them. They SHALL NOT be part of the shared Njord.Messages API contract.

#### Scenario: Pipeline source request is internal to consumers
- **WHEN** a consumer actor requests a SourceRef from PipelineActor
- **THEN** the request and response record types SHALL be defined in the consumer's or producer's feature library
- **AND** they SHALL NOT be in Njord.Messages.Pipeline

#### Scenario: MQTT sink request is internal to MQTT library
- **WHEN** MqttStateActor or MqttDiscoveryActor requests a connection to MqttConnectionActor
- **THEN** the request and response record types SHALL be defined within Njord.Mqtt
- **AND** they SHALL NOT appear in Njord.Messages

### Requirement: Mqtt message namespace

Messages related to MQTT connection lifecycle SHALL be defined in the `Njord.Messages.Mqtt` namespace within the Njord.Messages project. This SHALL include connection state notifications and inbound subscription management.

#### Scenario: MQTT connected notification is a shared message
- **WHEN** MqttConnectionActor establishes a broker connection
- **THEN** it SHALL notify listeners using a message type defined in Njord.Messages.Mqtt

#### Scenario: HA birth detection is a shared message
- **WHEN** MqttConnectionActor detects a Home Assistant birth message on the status topic
- **THEN** it SHALL notify listeners using a message type defined in Njord.Messages.Mqtt

#### Scenario: Inbound subscription is a shared message
- **WHEN** an actor wants to receive inbound MQTT messages from MqttConnectionActor
- **THEN** it SHALL send a subscription message defined in Njord.Messages.Mqtt

### Requirement: Enrichment message namespace

Messages for enrichment inter-actor communication SHALL be defined in the `Njord.Messages.Enrichment` namespace within the Njord.Messages project. This SHALL include snapshot recording and history query messages.

#### Scenario: History query is a shared message
- **WHEN** the history enrichment feature queries a ForecastHistoryActor for historical data
- **THEN** it SHALL use a query message type defined in Njord.Messages.Enrichment

#### Scenario: Snapshot recording is a shared message
- **WHEN** the enrichment pipeline sends a model snapshot to ForecastHistoryActor
- **THEN** it SHALL use a command message type defined in Njord.Messages.Enrichment

### Requirement: Stream shutdown messages are actor-internal

StopStreams, StreamsStopped, and StreamsStopFailed SHALL NOT be part of the shared Njord.Messages project. They SHALL be defined internally in the actors or host infrastructure that uses them for coordinated shutdown.

#### Scenario: Stream shutdown messages not in shared messages
- **WHEN** the Njord.Messages project is inspected
- **THEN** it SHALL NOT contain StopStreams, StreamsStopped, or StreamsStopFailed record definitions

#### Scenario: Coordinated shutdown still functions
- **WHEN** the actor system initiates coordinated shutdown
- **THEN** stream-owning actors SHALL still respond to shutdown requests using their internally defined message types
