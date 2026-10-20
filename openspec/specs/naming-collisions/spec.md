# naming-collisions Specification

## Purpose

Unique type names across the project — no two public types in the transitive dependency chain share an unqualified name.
## Requirements
### Requirement: Actor key for MQTT connection is unambiguous
The actor-key marker interface for the MQTT connection actor SHALL be named
`IMqttConnectionActor` to avoid collision with the transport interface
`Njord.Mqtt.Transport.IMqttConnection`.

#### Scenario: Host project references both without ambiguity
- **GIVEN** the host project imports both `Njord.Core.Actors` and `Njord.Mqtt.Transport`
- **WHEN** it resolves the MQTT actor key
- **THEN** it uses `IMqttConnectionActor` without disambiguation

### Requirement: Poll worker persistence records have unique names
The poll worker's persistence records (`PollingStarted`, `PollingStopped`, `ForecastChanged`, `ForecastUnchanged`,
`PollFailed`, `PollDeferred`, `PersistedPollWorkerState`) SHALL have names that no public type in `Njord.Messages`,
`Njord.Core` or `Njord.Pipeline` uses, so code referencing both the messages and the persistence records needs no
`using` alias.

#### Scenario: No ambiguity in projects referencing both Messages and Persistence
- **GIVEN** `Njord.Pipeline` references both `Njord.Messages` and `Njord.Persistence`
- **WHEN** it uses the poll worker's events and the polling messages (`StartPolling`, `StopPolling`, `TriggerTargetPoll`, `PollWorkerReport`)
- **THEN** every name resolves without a `using` alias

