## ADDED Requirements

### Requirement: State records match actor name

An actor's state record SHALL be named `<ActorName>State` and its extensions
`<ActorName>StateExtensions`.

#### Scenario: PollScheduler state
- **WHEN** looking up PollScheduler's state record
- **THEN** it SHALL be named `PollSchedulerState` with extensions in `PollSchedulerStateExtensions`

### Requirement: Actor classes do not use State suffix

The `*State` suffix is reserved for immutable state records. Actor classes
SHALL use domain-role names.

#### Scenario: MQTT state publisher actor
- **WHEN** the MQTT state publishing actor is referenced
- **THEN** it SHALL be named `MqttStatePublisher` (not `MqttState`) and its marker interface `IMqttStatePublisher`

### Requirement: One message file per command or query

Each command or query SHALL have its own file in `Njord.Messages/<Domain>/`.

#### Scenario: MQTT messages
- **WHEN** inspecting MQTT message files
- **THEN** `MqttConnected`, `SubscribeInbound`, and `MqttInboundMessage` SHALL each be in separate files

#### Scenario: SensorHub messages
- **WHEN** inspecting sensor hub message files
- **THEN** `UpdateReading` and `QuerySensorSnapshot` (with its response hierarchy) SHALL each be in separate files
