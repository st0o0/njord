## Purpose

Defines which messages belong in the shared Njord.Messages API (Mqtt and Enrichment namespaces) and which stream wiring and shutdown records stay actor-internal, keeping Akka.Streams types out of shared messages.
## Requirements
### Requirement: No Akka.Streams types in shared messages

The Njord.Messages project SHALL NOT have a package reference to any Akka package (including Akka and Akka.Streams). Its only project references SHALL be `Njord.Weather`, `Njord.Sensors` and `Njord.Compute`. No record in Njord.Messages SHALL reference `IActorRef`, `ISinkRef`, `ISourceRef` or any other Akka type. Records that carry such types SHALL live in `Njord.Core/Actors/` (shared stream handshakes) or privately inside the actor that uses them.

#### Scenario: Messages project has no Akka.Streams dependency
- **WHEN** the Njord.Messages.csproj is inspected
- **THEN** it SHALL NOT contain a PackageReference to any `Akka*` package

#### Scenario: SinkRef request records are actor-internal
- **WHEN** an actor needs to request a SinkRef from another actor
- **THEN** the request and response records SHALL be defined in `Njord.Core/Actors/` (shared handshake) or privately in the requesting library
- **AND** they SHALL NOT appear in the Njord.Messages project

#### Scenario: Forecast stream request is not a shared message
- **WHEN** a component requests a stream of forecasts
- **THEN** the request and response records SHALL be defined in `Njord.Core/Actors/`
- **AND** they SHALL NOT appear in the Njord.Messages project

### Requirement: Mqtt message namespace

Messages related to MQTT connection lifecycle SHALL be defined in the `Njord.Messages.Mqtt` namespace within the Njord.Messages project. This SHALL include connection state notifications and inbound subscription management.

#### Scenario: MQTT connected notification is a shared message
- **WHEN** MqttConnection establishes a broker connection
- **THEN** it SHALL notify listeners using a message type defined in Njord.Messages.Mqtt

#### Scenario: Inbound subscription is a shared message
- **WHEN** an actor wants to receive inbound MQTT messages from MqttConnection
- **THEN** it SHALL send a subscription message defined in Njord.Messages.Mqtt

### Requirement: Stream wiring records stay out of shared messages

Request/Response records for stream handshakes (requesting and providing SinkRefs or SourceRefs) SHALL NOT be part of the shared Njord.Messages contract. A handshake used by more than one library (pipeline output, forecast read-model stream, config stream) SHALL be defined in `Njord.Core/Actors/`. A handshake used inside one library SHALL be private or internal to that library.

#### Scenario: Pipeline source request is shared wiring
- **WHEN** a consumer actor requests a SourceRef from the forecast pipeline
- **THEN** the request and response record types SHALL be defined in `Njord.Core/Actors/`
- **AND** they SHALL NOT be in Njord.Messages

#### Scenario: MQTT sink request is internal to MQTT library
- **WHEN** an MQTT actor requests a connection to the MQTT connection actor
- **THEN** the request and response record types SHALL be defined within Njord.Mqtt
- **AND** they SHALL NOT appear in Njord.Messages

