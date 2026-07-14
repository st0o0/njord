# egress-stream-graph Delta Specification

## MODIFIED Requirements

### Requirement: MergeHub converges messages from multiple sources into a single publish sink
The `MqttConnectionActor` SHALL materialize a MergeHub of `MqttMessage`. The publish sink SHALL process messages with `SelectAsync(1)` through `IMqttTransport.SendAsync` with `Supervision.Directive.Resume` on errors. Multiple producers (MqttPublisherActor, DiscoveryActor, availability queue) SHALL feed the MergeHub via `SinkRef<MqttMessage>` or internal source queues. The availability source queue (online/offline) SHALL remain internal to `MqttConnectionActor`.

#### Scenario: Messages from multiple producers are serialized through the publish sink
- **WHEN** MqttPublisherActor and DiscoveryActor both push messages
- **THEN** all messages flow through the single MergeHub and are published via IMqttTransport

#### Scenario: Transport error resumes the stream
- **WHEN** `IMqttTransport.SendAsync` throws for one message
- **THEN** the stream resumes and processes subsequent messages

### Requirement: Pipeline SourceRef consumer maps FetchOutcome to state payloads
The `MqttPublisherActor` SHALL consume `FetchOutcome` from the pipeline BroadcastHub via SourceRef. It SHALL map `FetchOutcome.Success` to per-horizon state `MqttMessage` instances using `StatePayloadBuilder`, applying delta-publishing to skip unchanged payloads. The resulting messages SHALL flow into the MergeHub via the `SinkRef<MqttMessage>` obtained from `MqttConnectionActor`.

#### Scenario: Successful fetch produces state messages
- **WHEN** a `FetchOutcome.Success` arrives on the SourceRef
- **THEN** `MqttPublisherActor` produces per-horizon state messages on retained topics

#### Scenario: Delta publishing skips unchanged horizons
- **WHEN** the same payload was published in the previous cycle for a given (location, model, horizon)
- **THEN** no message is published for that horizon
