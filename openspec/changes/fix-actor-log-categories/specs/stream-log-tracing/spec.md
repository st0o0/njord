## MODIFIED Requirements

### Requirement: Stream tracing is activated through configuration
Stream log tracing SHALL be activatable by lowering the Serilog minimum level for the owning actor's log category (its full type name, e.g. `Njord.Mqtt.MqttStatePublisher`) to Debug in `appsettings*.json`, without code changes or recompilation.

#### Scenario: Enable MQTT state tracing
- **WHEN** the configuration sets the override `"Njord.Mqtt.MqttStatePublisher": "Debug"`
- **THEN** elements passing through `mqtt-state-in` and `mqtt-state-out` appear in console output

#### Scenario: Disable all tracing in production
- **WHEN** the default minimum level is Information and no Debug overrides are configured
- **THEN** zero `.Log()` stage messages appear in console output
