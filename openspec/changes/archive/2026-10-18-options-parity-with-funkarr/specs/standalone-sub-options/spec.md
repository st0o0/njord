## MODIFIED Requirements

### Requirement: Each sub-option has its own SectionName
Every options type SHALL be a standalone class with a `public const string SectionName` field and SHALL be bound exactly once, to that section, with startup validation. This SHALL include at least `NjordOptions` (forecast scope), `PollingOptions`, `OpenMeteoOptions`, `StorageOptions`, `MqttOptions`, `SensorOptions`, `PersistenceOptions`, `ConsensusOptions`, `AlertOptions`, `DerivedOptions`, `TrendOptions` and `IndexOptions`. No two options types SHALL bind the same section: `PollingOptions` binds `Njord:Polling`, `OpenMeteoOptions` binds `Njord:OpenMeteo` and `StorageOptions` binds `Njord:Storage`.

#### Scenario: MqttOptions binds to Njord:Mqtt section
- **WHEN** configuration contains `Njord:Mqtt:Host = "broker.local"`
- **THEN** `IOptions<MqttOptions>.Value.Host` SHALL equal `"broker.local"`

#### Scenario: Poll interval keeps its key
- **WHEN** environment variable `Njord__Polling__PollInterval=00:30:00` is set
- **THEN** `IOptionsMonitor<PollingOptions>.CurrentValue.PollInterval` SHALL equal 30 minutes

#### Scenario: Consumers inject only what they read
- **WHEN** any production class outside configuration setup is inspected
- **THEN** it injects only the options types whose values it reads
