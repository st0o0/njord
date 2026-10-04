## ADDED Requirements

### Requirement: Each sub-option is a standalone class with its own SectionName
`MqttOptions`, `GrpcOptions`, `SensorOptions`, `EnrichmentOptions`, and `PersistenceOptions` SHALL each be a standalone class with a `public const string SectionName` field (e.g., `"Njord:Mqtt"`). They SHALL NOT be nested inside `NjordOptions`.

#### Scenario: MqttOptions binds to Njord:Mqtt section
- **WHEN** configuration contains `Njord:Mqtt:Host = "broker.local"`
- **THEN** `IOptions<MqttOptions>.Value.Host` SHALL equal `"broker.local"`

#### Scenario: NjordOptions no longer contains sub-option properties
- **WHEN** `NjordOptions` is resolved from DI
- **THEN** it SHALL NOT contain properties for Mqtt, Grpc, Sensors, Enrichment, or Persistence sub-options

### Requirement: Each feature library registers its own sub-options
Each feature library's `AddNjord*()` extension method SHALL call `AddOptions<SubOptions>().Bind(configuration.GetSection(SubOptions.SectionName)).ValidateOnStart()` for its own sub-options.

#### Scenario: AddNjordMqtt registers MqttOptions
- **WHEN** `services.AddNjordMqtt()` is called
- **THEN** `MqttOptions` SHALL be resolvable from the service provider

#### Scenario: AddNjordGrpc registers GrpcOptions
- **WHEN** `services.AddNjordGrpc()` is called
- **THEN** `GrpcOptions` SHALL be resolvable from the service provider

### Requirement: Sub-option validators are co-located with their options class
Each sub-option class SHALL have its own `IValidateOptions<SubOptions>` validator registered alongside it, not in the host.

#### Scenario: ConsensusOptionsValidator validates ConsensusOptions
- **WHEN** invalid `ConsensusOptions` are provided (e.g., quorum < 1)
- **THEN** the `ConsensusOptionsValidator` SHALL produce a validation failure on `ConsensusOptions`, not on `NjordOptions`

### Requirement: Actors use IOptions of their specific sub-option
Actors and services SHALL inject `IOptions<SubOptions>` or `IOptionsMonitor<SubOptions>` for the sub-option they need, not `IOptions<NjordOptions>`.

#### Scenario: MqttConnectionActor uses IOptionsMonitor of MqttOptions
- **WHEN** `MqttConnectionActor` is constructed
- **THEN** it SHALL receive `IOptionsMonitor<MqttOptions>` for reactive config updates

#### Scenario: SensorHubActor uses IOptions of SensorOptions
- **WHEN** `SensorHubActor` is constructed
- **THEN** it SHALL receive `IOptions<SensorOptions>` for sensor fallback configuration
