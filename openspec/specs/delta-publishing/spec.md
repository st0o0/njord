# delta-publishing Specification

## Purpose

Delta publishing for the MQTT egress: the EgressActor's consumer graph skips unchanged horizon payloads to reduce broker traffic and HA entity updates, emitting only horizons whose values have actually changed since the last publish cycle.

## Requirements

### Requirement: Only publish horizons whose values have changed
`MqttEgressActor` SHALL perform HorizonProjection and delta-dedup for `PerModelUpdate` events. It SHALL maintain an in-memory cache of the last-published JSON payload per (location, model, horizon). Before publishing, it SHALL serialize the `ModelForecast` via `HorizonProjection.BuildPerHorizon`, compare each horizon's JSON against the cached value, and skip unchanged horizons. `HorizonProjection.BuildPerHorizon` SHALL omit individual parameter keys with null values from the JSON object, producing compact payloads. Entire horizons with no non-null values SHALL still be excluded from the result dictionary.

#### Scenario: MqttEgressActor serializes and deduplicates
- **WHEN** `MqttEgressActor` receives a `PerModelUpdate` with a `ModelForecast`
- **THEN** it SHALL call `HorizonProjection.BuildPerHorizon` to produce JSON, compare with cached values, and publish only changed horizons

#### Scenario: First cycle publishes all horizons
- **WHEN** the cache is empty (first cycle or after restart)
- **THEN** all horizons SHALL be published

#### Scenario: Unchanged horizon is skipped
- **WHEN** the JSON for a horizon is identical to the cached value
- **THEN** no MqttMessage SHALL be emitted for that horizon

#### Scenario: Changed horizon is published
- **WHEN** the h3 payload for (lucerne, icon_d2) differs from the last-published value
- **THEN** an MqttMessage is emitted and the cache is updated

#### Scenario: Null parameter keys stripped from JSON
- **WHEN** a horizon has temperature=15.2 and precipitation_probability=null
- **THEN** the JSON payload SHALL be `{"temperature":15.2}` without the null key

#### Scenario: All-null horizon excluded entirely
- **WHEN** a horizon's forecast point has no non-null parameter values
- **THEN** no entry for that horizon SHALL appear in the result dictionary

### Requirement: One PerModelUpdate produces multiple MqttMessages
For each `PerModelUpdate`, the `MqttEgressActor` SHALL produce one `MqttMessage` per horizon that has changed values. The number of messages per update ranges from 0 (all horizons unchanged) to the total configured horizons (first cycle or all values changed).

#### Scenario: Fan-out from one update to multiple messages
- **WHEN** a `PerModelUpdate` arrives for (lucerne, icon_d2) and 3 of 10 horizons have changed
- **THEN** exactly 3 MqttMessages are emitted
