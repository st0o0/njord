## Purpose

Defines naming rules for actor state records and actor classes, and the one-file-per-command-or-query layout for message files in Njord.Messages.
## Requirements
### Requirement: State records match actor name

An actor's state record SHALL be named `<ActorName>State` and its extensions
`<ActorName>StateExtensions`.

#### Scenario: PollWorker state
- **WHEN** looking up the state record of the persistent poll actor, `PollWorker`
- **THEN** it SHALL be named `PollWorkerState` with extensions in `PollWorkerStateExtensions`

### Requirement: Actor classes do not use State suffix

The `*State` suffix is reserved for immutable state records. Actor classes
SHALL use domain-role names.

#### Scenario: MQTT state publisher actor
- **WHEN** the MQTT state publishing actor is referenced
- **THEN** it SHALL be named `MqttStatePublisher` (not `MqttState`) and its marker interface `IMqttStatePublisher`

