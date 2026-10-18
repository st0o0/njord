# standalone-sub-options Specification

## Purpose

Lets every configuration section bind, validate and inject independently: each options type declares its own `SectionName`, has exactly one co-located validator, and is not nested inside `NjordOptions`.
## Requirements
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

### Requirement: Every options type has exactly one co-located validator
Every options type SHALL have exactly one `IValidateOptions<T>` validator for that type, defined next to the options class and registered by the same setup extension that binds it. No validator SHALL validate a different options type than the one it is registered for.

#### Scenario: Consensus validator validates ConsensusOptions
- **WHEN** invalid consensus settings are provided (e.g., quorum < 1)
- **THEN** the consensus validator SHALL fail startup validation of `ConsensusOptions`

#### Scenario: Mqtt validator validates MqttOptions
- **WHEN** `Njord:Mqtt:Enabled` is `true` and `Njord:Mqtt:Host` is missing
- **THEN** the MQTT validator SHALL fail startup validation of `MqttOptions` naming the missing host

### Requirement: Options types are not nested inside NjordOptions
No options type SHALL be a property of another options type. `NjordOptions` SHALL contain only the forecast scope: locations, default models, horizons, forecast days and the requested parameter set. `SensorOptions` SHALL be registered and validated by `Njord.SensorHub`'s setup container, not the host. There is no `GrpcOptions` type.

#### Scenario: NjordOptions has no nested sections
- **WHEN** `NjordOptions` is resolved from DI
- **THEN** it SHALL NOT contain properties for `Grpc`, `Sensors`, `Mqtt`, `Enrichment`, `Persistence`, `PollInterval` or `BudgetOverride`

#### Scenario: SensorHub uses IOptions of SensorOptions
- **WHEN** the `SensorHub` actor is constructed
- **THEN** it receives `IOptions<SensorOptions>` for its staleness configuration

