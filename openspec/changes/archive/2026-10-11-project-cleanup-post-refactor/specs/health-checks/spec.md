## MODIFIED Requirements

### Requirement: Shared health state is writable by actors
A `NjordHealthState` class SHALL be registered as a DI singleton. Actors
SHALL write to it using thread-safe operations. Health check classes SHALL
read from it. No health check SHALL send messages to actors.

`NjordHealthState` SHALL track: `IsMqttConnected` (bool),
`MqttDisconnectedSince` (DateTimeOffset?), `LastSuccessfulPollUtc`
(DateTimeOffset?), `ServiceStartedUtc`, budget usage, and budget limits.

`SetMqttConnected` SHALL set `IsMqttConnected` to true and clear
`MqttDisconnectedSince`. It SHALL NOT track a connected-since timestamp.

#### Scenario: Actor updates health state
- **WHEN** MqttConnectionActor connects successfully
- **THEN** `NjordHealthState.IsMqttConnected` is set to `true` and
  `MqttDisconnectedSince` is cleared

#### Scenario: Health check reads shared state
- **WHEN** a health check executes
- **THEN** it reads from `NjordHealthState` without sending any actor message

#### Scenario: No MqttConnectedSince property exists
- **WHEN** the `NjordHealthState` API is inspected
- **THEN** there SHALL be no `MqttConnectedSince` property or backing field
