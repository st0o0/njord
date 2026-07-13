# mqtt-egress Specification (Delta)

## Purpose

Extends the MqttEgressActor with a SinkRef vending protocol so external actors (EnrichmentActor) can push computed MqttMessages into the existing MergeHub transport. Also extends discovery to cover the consensus pseudo-model device.

## ADDED Requirements

### Requirement: The egress actor vends a SinkRef for external message producers
The `MqttEgressActor` SHALL respond to a `RequestMqttSink` message with a `MqttSinkResponse` containing a `SinkRef<MqttMessage>` connected to the existing MergeHub. Multiple SinkRefs MAY be vended — each connects as an independent producer to the MergeHub. The SinkRef SHALL be vended only after the egress graph is materialized.

#### Scenario: EnrichmentActor requests and receives a SinkRef
- **WHEN** the EnrichmentActor sends `RequestMqttSink` to the MqttEgressActor after the egress graph is materialized
- **THEN** the MqttEgressActor responds with a `MqttSinkResponse` containing a `SinkRef<MqttMessage>`

#### Scenario: SinkRef messages flow through the same publish path
- **WHEN** the EnrichmentActor sends an `MqttMessage` via the SinkRef
- **THEN** the message flows through the MergeHub and is published by the same Publish Sink as state and discovery messages

#### Scenario: Request before materialization is stashed
- **WHEN** a `RequestMqttSink` arrives before the egress graph is materialized
- **THEN** the message is stashed and processed after materialization

### Requirement: Discovery covers the consensus pseudo-model device
When `DiscoveryEnabled` is `true` and the consensus enrichment is enabled, the egress actor SHALL publish a retained device-based discovery payload for each configured location's consensus device (`njord_{location}_consensus`). The payload SHALL carry the same horizons and parameters as model devices, plus diagnostic attributes. Discovery SHALL be published alongside model device discovery on startup and HA birth.

#### Scenario: Consensus device discovery at startup
- **WHEN** the broker connects and consensus enrichment is enabled
- **THEN** one discovery payload per location is published for the consensus device alongside model device payloads

#### Scenario: Consensus discovery on HA birth
- **WHEN** `homeassistant/status` receives `online` and consensus is enabled
- **THEN** the consensus device discovery payloads are re-published

#### Scenario: Consensus disabled skips discovery
- **WHEN** `EnrichmentOptions.Consensus.Enabled` is `false`
- **THEN** no consensus device discovery payloads are published

## MODIFIED Requirements

### Requirement: The egress actor exposes a SinkRef for internal use only
The egress actor SHALL materialize a `SinkRef<MqttMessage>` connected to the MergeHub for internal use by its consumer graph. The egress actor SHALL also vend SinkRefs to external actors on request via the `RequestMqttSink` / `MqttSinkResponse` protocol. External SinkRefs connect as additional producers to the same MergeHub.

#### Scenario: Internal consumer graph feeds into MergeHub
- **WHEN** the egress consumer graph processes a `FetchOutcome.Success`
- **THEN** the resulting `MqttMessage`(s) flow into the MergeHub through the internal consumer connection

#### Scenario: External SinkRef feeds into same MergeHub
- **WHEN** the EnrichmentActor sends messages via a vended SinkRef
- **THEN** the messages merge into the same MergeHub alongside internal messages
