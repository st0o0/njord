## MODIFIED Requirements

### Requirement: Egress metrics
The MQTT layer SHALL record on the `Njord.Mqtt` meter:
- `njord_mqtt_dedup_total` (Counter): incremented on each publish decision of `MqttStatePublisher`, with labels `location`, `decision` (published, skipped). `published` SHALL be counted only after the message was sent successfully.
- `njord_mqtt_publish_failures_total` (Counter): incremented for each state message whose publish failed or timed out, with label `location`.
- `njord_mqtt_connected` (Gauge): 1 when `MqttConnection` is connected, 0 after a disconnect. When MQTT is disabled the gauge is never recorded.

#### Scenario: Dedup decision is counted
- **WHEN** `MqttStatePublisher` skips a publish because the payload hash matches
- **THEN** `njord_mqtt_dedup_total{location="lucerne",decision="skipped"}` is incremented by 1

#### Scenario: New data is published
- **WHEN** `MqttStatePublisher` publishes because the payload hash differs
- **THEN** `njord_mqtt_dedup_total{location="lucerne",decision="published"}` is incremented by 1

#### Scenario: MQTT connection state is exposed
- **WHEN** `MqttConnection` connects successfully
- **THEN** `njord_mqtt_connected` is set to 1

#### Scenario: MQTT disconnection is exposed
- **WHEN** `MqttConnection` loses the connection
- **THEN** `njord_mqtt_connected` is set to 0

#### Scenario: Publish failure is counted
- **WHEN** the publish of a state message for location "lucerne" fails
- **THEN** `njord_mqtt_publish_failures_total{location="lucerne"}` is incremented by 1
- **AND** `njord_mqtt_dedup_total{location="lucerne",decision="published"}` is not incremented
