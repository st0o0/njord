## MODIFIED Requirements

### Requirement: State updates keyed by entity ID
Each `EntityStateUpdate` SHALL carry the `entity_id` from the manifest and an `updated_at` timestamp. The value SHALL be a oneof of the OutputKind-specific value types: `WeatherValue`, `AlertValue`, `ScoreValue`, `ConditionValue`, `TrendValue`, `ForecastValue`, `ObservationValue`. The entity lookup SHALL resolve Observation results by their `FeatureName` field rather than by CLR type, since all observation features share `ObservationResult`.

#### Scenario: Alert state update
- **WHEN** the FrostAlert feature produces a result for location Vienna
- **THEN** the stream SHALL emit an `EntityStateUpdate` with `entity_id = "vienna_frost"` and an `AlertValue`

#### Scenario: Observation state update
- **WHEN** the PM2.5 observation feature produces a result for location Berlin
- **THEN** the stream SHALL emit an `EntityStateUpdate` with `entity_id = "berlin_pm25"` and an `ObservationValue`

#### Scenario: Multiple observation features produce distinct entity updates
- **WHEN** both PM2.5 and WaveHeight observation features produce results for location Vienna
- **THEN** the stream SHALL emit two distinct `EntityStateUpdate` messages with `entity_id = "vienna_pm25"` and `entity_id = "vienna_wave_height"` respectively
