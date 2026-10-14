# egress-stream-graph Specification

## Purpose

Defines the MQTT publish protocol and availability path: `MqttMessage` is the single message type that `MqttStatePublisher` hands to the transport, and `MqttConnection` publishes availability directly on connect and stop.

## Requirements
### Requirement: MqttMessage is the unified publish protocol
All messages destined for the broker SHALL be expressed as `MqttMessage(string Topic, string Payload, bool Retain)`. The publish path SHALL be content-agnostic — it does not interpret topic or payload semantics.

#### Scenario: MqttMessage carries topic, payload, and retain flag
- **WHEN** an `MqttMessage("njord/home/icon_d2/state", "{...}", true)` is published
- **THEN** the transport publishes to topic `njord/home/icon_d2/state` with the given payload and retain=true

### Requirement: Availability is published directly on connect and stop
`MqttConnection` SHALL publish a retained `online` on `{BaseTopic}/status` after every successful connect and a retained `offline` when it stops, by calling `IMqttTransport.SendAsync` directly. No queue or MergeHub SHALL be involved. The broker-side Last Will (`offline`, retained) SHALL cover connection loss.

#### Scenario: Online on connect
- **WHEN** the broker connection succeeds
- **THEN** `online` is published retained on the availability topic

#### Scenario: Offline on shutdown
- **WHEN** the actor stops
- **THEN** `offline` is published retained on the availability topic

