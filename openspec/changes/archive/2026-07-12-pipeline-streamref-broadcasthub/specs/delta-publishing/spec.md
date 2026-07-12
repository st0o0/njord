## MODIFIED Requirements

### Requirement: Only publish horizons whose values have changed
The EgressActor's consumer graph SHALL maintain an in-memory cache of the last-published payload per (location, model, horizon). Before publishing, it SHALL compare the new payload against the cached value. If the payloads are identical (string equality), the publish SHALL be skipped. On first cycle after startup (empty cache), all horizons SHALL publish.

#### Scenario: Unchanged horizon is skipped
- **WHEN** the h72 payload for (lucerne, icon_d2) is identical to the last-published value
- **THEN** no MqttMessage is emitted for that horizon

#### Scenario: Changed horizon is published
- **WHEN** the h3 payload for (lucerne, icon_d2) differs from the last-published value
- **THEN** an MqttMessage is emitted and the cache is updated

#### Scenario: First cycle publishes all horizons
- **WHEN** the egress consumer starts with an empty cache
- **THEN** all horizons for all fetched devices are published

#### Scenario: EgressActor restart clears the cache
- **WHEN** the EgressActor restarts
- **THEN** the cache is empty and the next cycle publishes all horizons

### Requirement: One FetchOutcome produces multiple MqttMessages
For each `FetchOutcome.Success`, the egress consumer graph SHALL produce one `MqttMessage` per horizon that has changed values. The number of messages per outcome ranges from 0 (all horizons unchanged) to the total configured horizons (first cycle or all values changed).

#### Scenario: Fan-out from one fetch to multiple messages
- **WHEN** a fetch succeeds for (lucerne, icon_d2) and 3 of 10 horizons have changed
- **THEN** exactly 3 MqttMessages are emitted into the egress MergeHub
