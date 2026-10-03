## MODIFIED Requirements

### Requirement: Only publish horizons whose values have changed
`MqttEgressActor` SHALL perform HorizonProjection and delta-dedup for `PerModelUpdate` events. It SHALL maintain an in-memory cache of the last-published JSON payload per (location, model, horizon). Before publishing, it SHALL serialize the `ModelForecast` via `HorizonProjection.BuildPerHorizon`, compare each horizon's JSON against the cached value, and skip unchanged horizons.

#### Scenario: MqttEgressActor serializes and deduplicates
- **WHEN** `MqttEgressActor` receives a `PerModelUpdate` with a `ModelForecast`
- **THEN** it SHALL call `HorizonProjection.BuildPerHorizon` to produce JSON, compare with cached values, and publish only changed horizons

#### Scenario: First cycle publishes all horizons
- **WHEN** the cache is empty (first cycle or after restart)
- **THEN** all horizons SHALL be published

#### Scenario: Unchanged horizon is skipped
- **WHEN** the JSON for a horizon is identical to the cached value
- **THEN** no MqttMessage SHALL be emitted for that horizon
