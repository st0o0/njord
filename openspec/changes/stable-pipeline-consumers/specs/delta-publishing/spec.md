## MODIFIED Requirements

### Requirement: Only changed state payloads are published
`MqttStatePublisher` SHALL keep, per MQTT topic, the hash of the last successfully published payload for the lifetime of one pipeline subscription (a new subscription after a re-attach starts with an empty cache), and SHALL skip every mapped message whose payload hash equals the cached one, counting the decision in the `njord_mqtt_dedup_total` counter. The cache SHALL be updated only after the publish of a message succeeded; a message whose publish failed SHALL leave the cache unchanged, so the same payload is published again on the next cycle. For `ForecastReceived` it SHALL build payloads with `HorizonProjection.BuildPerHorizon`, which SHALL omit parameter keys whose value is null and SHALL exclude horizons without any non-null value.

#### Scenario: First cycle publishes all horizons
- **WHEN** the cache is empty (first cycle, or the first cycle of a new subscription)
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

#### Scenario: Failed payload is published again on the next cycle
- **WHEN** the publish of a payload fails and the next cycle maps the identical payload for the same topic
- **THEN** the payload is published again instead of being skipped as unchanged
