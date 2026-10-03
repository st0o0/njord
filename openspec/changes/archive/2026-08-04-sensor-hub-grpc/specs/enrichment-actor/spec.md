## MODIFIED Requirements

### Requirement: EnrichmentActor resolves SensorHub dependency
The EnrichmentActor SHALL resolve the SensorHub actor as an additional dependency alongside PipelineActor and EgressActor. The stream SHALL NOT materialize until all three dependencies are resolved.

#### Scenario: SensorHub resolved
- **WHEN** the SensorHub actor is registered in the ActorRegistry
- **THEN** the EnrichmentActor SHALL resolve it and include it in dependency tracking

#### Scenario: SensorHub unavailable at startup
- **WHEN** the SensorHub actor is not yet available
- **THEN** the EnrichmentActor SHALL retry resolution using the standard retry mechanism

### Requirement: EnrichmentActor pulls SensorSnapshot per location
Before computing enrichments for a location, the EnrichmentActor SHALL send a `GetSnapshot(location)` message to the SensorHub and pass the resulting `SensorSnapshot?` to all enrichment `Compute` calls. The Ask SHALL use a short timeout (1 second). If the SensorHub does not respond, a null snapshot SHALL be used.

#### Scenario: SensorHub responds with data
- **WHEN** the SensorHub has readings for location "Luzern"
- **THEN** the enrichments SHALL receive a non-null `SensorSnapshot` with those readings

#### Scenario: SensorHub responds with no data
- **WHEN** the SensorHub has no readings for location "Luzern"
- **THEN** the enrichments SHALL receive a null `SensorSnapshot`

#### Scenario: SensorHub Ask times out
- **WHEN** the SensorHub does not respond within 1 second
- **THEN** the enrichments SHALL receive a null `SensorSnapshot` and processing SHALL continue
