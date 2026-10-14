## ADDED Requirements

### Requirement: Only changed state payloads are published
`MqttStatePublisher` SHALL keep, per MQTT topic, the hash of the last published payload for the lifetime of its stream graph, and SHALL skip every mapped message whose payload hash equals the cached one, counting the decision in the `njord_mqtt_dedup_total` counter. For `ForecastReceived` it SHALL build payloads with `HorizonProjection.BuildPerHorizon`, which SHALL omit parameter keys whose value is null and SHALL exclude horizons without any non-null value.

#### Scenario: First cycle publishes all horizons
- **WHEN** the cache is empty (first cycle or after a restart of the graph)
- **THEN** all horizons with values are published

#### Scenario: Unchanged horizon is skipped
- **WHEN** the payload for a horizon topic is identical to the last published one
- **THEN** no message is published for that topic

#### Scenario: Changed horizon is published
- **WHEN** the h3 payload for (lucerne, icon_d2) differs from the last published one
- **THEN** the message is published and the cache is updated

#### Scenario: Null parameter keys stripped from JSON
- **WHEN** a horizon has temperature=15.2 and precipitation_probability=null
- **THEN** the JSON payload is `{"temperature":15.2}` without the null key

#### Scenario: All-null horizon excluded entirely
- **WHEN** a horizon's forecast point has no non-null parameter values
- **THEN** no entry for that horizon appears in the result dictionary

### Requirement: One ForecastReceived produces one message per changed horizon
For each `PipelineEvent.ForecastReceived`, `MqttStatePublisher` SHALL publish one retained `MqttMessage` per horizon whose payload changed. The number of messages ranges from 0 (all horizons unchanged) to the number of applicable horizons (first cycle or all values changed).

#### Scenario: Fan-out from one update to multiple messages
- **WHEN** a `ForecastReceived` arrives for (lucerne, icon_d2) and 3 of 10 horizons changed
- **THEN** exactly 3 messages are published

## REMOVED Requirements

### Requirement: Only publish horizons whose values have changed
**Reason**: The requirement named types, files or behavior that no longer exist; it is restated against the current code.
**Migration**: Superseded by "Only changed state payloads are published" in this capability.

### Requirement: One PerModelUpdate produces multiple MqttMessages
**Reason**: The requirement named types, files or behavior that no longer exist; it is restated against the current code.
**Migration**: Superseded by "One ForecastReceived produces one message per changed horizon" in this capability.
