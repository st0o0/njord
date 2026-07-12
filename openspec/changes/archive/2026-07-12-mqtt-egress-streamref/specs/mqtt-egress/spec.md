## MODIFIED Requirements

### Requirement: The connection actor owns the broker lifecycle
An actor SHALL own the MQTT connection: connect at startup, reconnect with exponential backoff, register a Last Will that publishes `offline` (retained) to the service availability topic. The actor SHALL materialize the egress stream graph (MergeHub → Publish Sink) using `Context.Materializer()` so the graph lifecycle is bound to the actor. Egress failures MUST NOT crash the pipeline actor.

#### Scenario: Last Will announces service death
- **WHEN** njord's connection dies without a clean disconnect
- **THEN** the broker publishes retained `offline` on the service availability topic

#### Scenario: Reconnect restores availability
- **WHEN** the broker becomes reachable again after an outage
- **THEN** the actor reconnects with backoff and offers retained `online` into the availability queue

#### Scenario: Actor-bound graph lifecycle
- **WHEN** the egress actor stops
- **THEN** the egress stream graph (MergeHub + Publish Sink) terminates

### Requirement: Telemetry publishes one retained state per device per cycle
For every successful fetch the pipeline SHALL produce one `MqttMessage` per (location, model) device and deliver it to the egress via StreamRef. The egress MergeHub publishes it to the broker as a retained state JSON. Devices whose fetch failed SHALL NOT be published that cycle.

#### Scenario: State arrives via StreamRef
- **WHEN** the pipeline emits an MqttMessage for device njord_lucerne_icon_d2
- **THEN** the egress publishes it retained to `njord/lucerne/icon_d2/state`

#### Scenario: Failed models produce no state message
- **WHEN** a model's fetch fails
- **THEN** no MqttMessage for that device enters the StreamRef

## REMOVED Requirements

### Requirement: Telemetry publishes one retained state per device per cycle
**Reason:** The original requirement specified that the "system SHALL publish one retained state JSON per (location, model) device that delivered a forecast" with the actor as publisher. The telemetry publish responsibility has moved from the egress actor receiving messages directly to the pipeline producing MqttMessages that flow through the StreamRef into the egress MergeHub. The new MODIFIED requirement above replaces this with the StreamRef-based flow.
**Migration:** Remove `PublishTelemetry` handling (already done). State payloads now arrive as `MqttMessage` via the egress MergeHub's StreamRef input, not as actor messages.
