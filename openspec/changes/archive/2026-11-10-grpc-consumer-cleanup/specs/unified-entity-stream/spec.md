## MODIFIED Requirements

### Requirement: Snapshot on connect
When a client calls `Subscribe`, the server SHALL immediately send the current state of
every registered entity as a batch of `EntityStateUpdate` messages before any live updates.
Entities that have not yet received a result from the pipeline SHALL be included with a
default value appropriate to their OutputKind (alerts with Severity.None, conditions with
Active=false, scores with score 0, observations with value 0). The snapshot SHALL reflect
the full entity set from the manifest, not only entities that have produced results.

#### Scenario: New subscriber receives current state
- **WHEN** a client subscribes and 10 entities already have values
- **THEN** the client SHALL receive `EntityStateUpdate` messages for all registered entities,
  including those with default values

#### Scenario: Non-triggered alerts appear as cleared
- **WHEN** a client subscribes and 30 alert entities are registered but only 3 have triggered
- **THEN** the snapshot SHALL contain 30 alert `EntityStateUpdate` messages: 3 with their
  actual severity and 27 with `AlertSeverity.None`

#### Scenario: Reconnecting client receives fresh snapshot
- **WHEN** a client disconnects and reconnects
- **THEN** it SHALL receive a complete fresh snapshot, not a diff from the previous connection

### Requirement: State updates keyed by entity ID
Each `EntityStateUpdate` SHALL carry the `entity_id` from the manifest and an `updated_at`
timestamp. The value SHALL be a oneof of the OutputKind-specific value types: `WeatherValue`,
`AlertValue`, `ScoreValue`, `ConditionValue`, `TrendValue`, `ForecastValue`,
`ObservationValue`. The entity lookup SHALL resolve results by feature descriptor name and
location, not by CLR type.

#### Scenario: Alert state update
- **WHEN** the FrostAlert feature produces a result for location Vienna
- **THEN** the stream SHALL emit an `EntityStateUpdate` with `entity_id = "vienna_frost"` and an `AlertValue`

#### Scenario: Observation state update
- **WHEN** the PM2.5 observation feature produces a result for location Berlin
- **THEN** the stream SHALL emit an `EntityStateUpdate` with `entity_id = "berlin_pm25"` and an `ObservationValue`

#### Scenario: Multiple observation features produce distinct entity updates
- **WHEN** both PM2.5 and WaveHeight observation features produce results for location Vienna
- **THEN** the stream SHALL emit two distinct `EntityStateUpdate` messages with `entity_id = "vienna_pm25"` and `entity_id = "vienna_wave_height"` respectively
