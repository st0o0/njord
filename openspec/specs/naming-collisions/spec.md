# naming-collisions Specification

## Purpose

Unique type names across the project — no two public types in the transitive dependency chain share an unqualified name.
## Requirements
### Requirement: Persistence event DataChanged has unique name
The persistence event record for poll-scheduler data changes SHALL be named
`PersistedDataChanged` to avoid collision with the actor message
`Njord.Messages.Pipeline.DataChanged`. Wire-format `[JsonProperty]` names
SHALL remain unchanged.

#### Scenario: No ambiguity in projects referencing both Messages and Persistence
- **GIVEN** a project that transitively references both `Njord.Messages` and `Njord.Persistence`
- **WHEN** it uses `DataChanged`
- **THEN** the compiler resolves to `Njord.Messages.Pipeline.DataChanged` without a `using` alias

### Requirement: Actor key for MQTT connection is unambiguous
The actor-key marker interface for the MQTT connection actor SHALL be named
`IMqttConnectionActor` to avoid collision with the transport interface
`Njord.Mqtt.Transport.IMqttConnection`.

#### Scenario: Host project references both without ambiguity
- **GIVEN** the host project imports both `Njord.Core.Actors` and `Njord.Mqtt.Transport`
- **WHEN** it resolves the MQTT actor key
- **THEN** it uses `IMqttConnectionActor` without disambiguation

### Requirement: Trend math class name does not collide with the analyzer
The static class holding pure trend math SHALL be named `TrendFormulas` (following the `<Feature>Formulas` scheme) and live next to the DI-registered `TrendAnalyzer` in `Njord.Trends`, so no two public types share the name `TrendAnalyzer`.

#### Scenario: Pipeline references the analyzer without ambiguity
- **GIVEN** `Njord.Pipeline` references `Njord.Compute` and `Njord.Trends`
- **WHEN** it references `TrendAnalyzer`
- **THEN** exactly one public type named `TrendAnalyzer` exists, `Njord.Trends.TrendAnalyzer`

