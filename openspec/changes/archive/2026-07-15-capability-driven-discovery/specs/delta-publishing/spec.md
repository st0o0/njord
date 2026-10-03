## MODIFIED Requirements

### Requirement: Only publish horizons whose values have changed
The EgressActor's consumer graph SHALL maintain an in-memory cache of the last-published payload per (location, model, horizon). Before publishing, it SHALL compare the new payload against the cached value. If the payloads are identical (string equality), the publish SHALL be skipped. On first cycle after startup (empty cache), all horizons SHALL publish. `HorizonProjection.BuildPerHorizon` SHALL omit individual parameter keys with null values from the JSON object, producing compact payloads. Entire horizons with no non-null values SHALL still be excluded from the result dictionary.

#### Scenario: Unchanged horizon is skipped
- **WHEN** the h72 payload for (lucerne, ecmwf_ifs025) is identical to the last-published value
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

#### Scenario: Null parameter keys stripped from JSON
- **WHEN** a horizon has temperature=15.2 and precipitation_probability=null
- **THEN** the JSON payload SHALL be `{"temperature":15.2}` without the null key

#### Scenario: All-null horizon excluded entirely
- **WHEN** a horizon's forecast point has no non-null parameter values
- **THEN** no entry for that horizon SHALL appear in the result dictionary
