## Purpose

Defines which messages belong in the shared Njord.Messages project. All inter-actor messages including stream handshakes live in Messages. Njord.Messages references Akka.Streams for ISinkRef/ISourceRef but not Akka.Actor or other actor-infrastructure packages.
## Requirements
### Requirement: Mqtt message namespace

Messages related to MQTT connection lifecycle SHALL be defined in the `Njord.Messages.Mqtt` namespace within the Njord.Messages project. This SHALL include connection state notifications and inbound subscription management.

#### Scenario: MQTT connected notification is a shared message
- **WHEN** MqttConnection establishes a broker connection
- **THEN** it SHALL notify listeners using a message type defined in Njord.Messages.Mqtt

#### Scenario: Inbound subscription is a shared message
- **WHEN** an actor wants to receive inbound MQTT messages from MqttConnection
- **THEN** it SHALL send a subscription message defined in Njord.Messages.Mqtt

### Requirement: Njord.Messages package dependencies
Njord.Messages SHALL reference the Akka.Streams NuGet package to support ISinkRef<T> and ISourceRef<T> in stream handshake message types. It SHALL NOT reference Akka.Actor, Akka.Cluster, Akka.Persistence, or Akka.Hosting.

#### Scenario: Messages can define stream-ref message types
- **WHEN** a message record carries an ISinkRef<DomainDataReceived> or ISourceRef<PipelineEvent> field
- **THEN** it compiles in Njord.Messages without errors

#### Scenario: Messages does not depend on actor infrastructure
- **WHEN** Njord.Messages project references are inspected
- **THEN** it has no reference to Akka.Actor, Akka.Remote, Akka.Cluster, Akka.Persistence, or Akka.Hosting

### Requirement: Stream handshakes live in Njord.Messages
Request/Response records for stream handshakes (requesting and providing SinkRefs or SourceRefs) SHALL be defined in `Njord.Messages.Stream`. This includes RequestForecastSink*, RequestPipelineOutput*, and RequestGrpcStream* families.

#### Scenario: Pipeline source request is in Messages
- **WHEN** a consumer actor requests a SourceRef from the forecast pipeline
- **THEN** the request and response record types SHALL be defined in `Njord.Messages.Stream`

### Requirement: Analysis contracts live in Results sub-namespaces of Njord.Messages
The result records and result enums SHALL be declared in `Njord.Messages.Results` with sub-namespaces per OutputKind: `Results.Alerts`, `Results.Scores`, `Results.Conditions`, `Results.Trends`, `Results.Forecasts`. No separate contracts assembly SHALL exist per feature.

#### Scenario: Contract type location
- **WHEN** an alert result record is inspected
- **THEN** its namespace is `Njord.Messages.Results.Alerts` and its assembly is `Njord.Messages`

#### Scenario: No per-feature contracts assembly
- **WHEN** the solution is inspected
- **THEN** no project named `Njord.<Feature>.Contracts` exists

