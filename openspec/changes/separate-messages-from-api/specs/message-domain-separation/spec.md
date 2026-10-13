## MODIFIED Requirements

### Requirement: No Akka.Streams types in shared messages

The Njord.Messages project SHALL NOT have a package reference to any Akka package (including Akka and Akka.Streams). Its only project references SHALL be `Njord.Weather`, `Njord.Sensors` and `Njord.Compute`. No record in Njord.Messages SHALL reference `IActorRef`, `ISinkRef`, `ISourceRef` or any other Akka type. Records that carry such types SHALL live in `Njord.Core/Actors/` (shared stream handshakes) or privately inside the actor that uses them.

#### Scenario: Messages project has no Akka dependency
- **WHEN** the Njord.Messages.csproj is inspected
- **THEN** it SHALL NOT contain a PackageReference to any `Akka*` package

#### Scenario: Forecast stream request is not a shared message
- **WHEN** a component requests a stream of forecasts
- **THEN** the request and response records SHALL be defined in `Njord.Core/Actors/`
- **AND** they SHALL NOT appear in the Njord.Messages project

### Requirement: Stream wiring records are actor-internal

Request/Response records for stream handshakes (requesting and providing SinkRefs or SourceRefs) SHALL NOT be part of the shared Njord.Messages contract. A handshake used by more than one library (pipeline output, forecast read-model stream, config stream) SHALL be defined in `Njord.Core/Actors/`. A handshake used inside one library SHALL be private or internal to that library.

#### Scenario: Pipeline source request is shared wiring
- **WHEN** a consumer actor requests a SourceRef from the forecast pipeline
- **THEN** the request and response record types SHALL be defined in `Njord.Core/Actors/`
- **AND** they SHALL NOT be in Njord.Messages

#### Scenario: MQTT sink request is internal to MQTT library
- **WHEN** an MQTT actor requests a connection to the MQTT connection actor
- **THEN** the request and response record types SHALL be defined within Njord.Mqtt
- **AND** they SHALL NOT appear in Njord.Messages

## REMOVED Requirements

### Requirement: Enrichment message namespace
**Reason**: The history feature and its snapshot/query messages no longer exist. Analysis results travel as `PipelineEvent` variants.
**Migration**: Analysis events live in `Njord.Messages/Pipeline/`; there is no `Enrichment` message namespace.
