# naming-collisions Delta

## MODIFIED Requirements

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

### Requirement: Compute-layer trend functions have unique name
The static compute-function class for trend analysis SHALL be named
`TrendComputer` (consistent with `ConsensusComputer`, `AlertComputer`) to
avoid collision with the DI-injected `Njord.Trends.TrendAnalyzer`.

#### Scenario: Pipeline references both Compute and Trends without ambiguity
- **GIVEN** `Njord.Pipeline` imports both `Njord.Compute.Analysis` and `Njord.Trends`
- **WHEN** it references `TrendAnalyzer`
- **THEN** the compiler resolves to `Njord.Trends.TrendAnalyzer` without disambiguation
