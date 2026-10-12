## ADDED Requirements

### Requirement: MqttNetPublisher uses IOptions of MqttOptions

`MqttNetPublisher` SHALL accept `IOptions<MqttOptions>` via constructor
injection instead of a raw `MqttOptions` instance. The manual factory in
`MqttSetupContainer` that extracts `.Value` and passes it as raw options
SHALL be removed.

#### Scenario: MqttNetPublisher resolves via standard DI

- **WHEN** `MqttNetPublisher` is resolved from DI
- **THEN** its constructor SHALL have received `IOptions<MqttOptions>`,
  not a manually extracted `MqttOptions` value

### Requirement: Mqtt enabled check uses bound options

Setup containers that check whether MQTT is enabled SHALL read the value
from `IOptions<MqttOptions>.Value.Enabled` (or an equivalent options-based
path), not from raw `IConfiguration.GetSection(...).GetValue(...)`.

#### Scenario: MqttSetupContainer reads Enabled from options

- **WHEN** `MqttSetupContainer` checks whether MQTT is enabled
- **THEN** it SHALL resolve `MqttOptions` from the bound options,
  not read `Njord:Mqtt:Enabled` from raw `IConfiguration`

#### Scenario: CoreSetupContainer reads Mqtt Enabled from options

- **WHEN** `CoreSetupContainer` checks whether MQTT is enabled
- **THEN** it SHALL resolve `MqttOptions` from the bound options,
  not read `Njord:Mqtt:Enabled` from raw `IConfiguration`
