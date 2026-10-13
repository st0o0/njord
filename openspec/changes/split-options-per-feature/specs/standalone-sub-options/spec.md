## MODIFIED Requirements

### Requirement: Each sub-option has its own SectionName
Every options type SHALL be a standalone class with a `public const string SectionName` field and SHALL be bound exactly once, to that section, with startup validation. This SHALL include at least `NjordOptions` (forecast scope), `PollingOptions`, `MqttOptions`, `GrpcOptions`, `SensorOptions`, `PersistenceOptions`, `ConsensusOptions`, `AlertOptions`, `DerivedOptions`, `TrendOptions` and `IndexOptions`. Configuration keys and environment variable names SHALL be unchanged; two options types MAY bind the same section when each reads a disjoint set of keys (e.g. `NjordOptions` and `PollingOptions` both bind `Njord`).

#### Scenario: MqttOptions binds to Njord:Mqtt section
- **WHEN** configuration contains `Njord:Mqtt:Host = "broker.local"`
- **THEN** `IOptions<MqttOptions>.Value.Host` SHALL equal `"broker.local"`

#### Scenario: Poll interval keeps its key
- **WHEN** environment variable `Njord__PollInterval=00:30:00` is set
- **THEN** `IOptions<PollingOptions>.Value.PollInterval` SHALL equal 30 minutes

#### Scenario: Consumers inject only what they read
- **WHEN** any production class outside configuration setup is inspected
- **THEN** it injects only the options types whose values it reads

### Requirement: GrpcOptions and SensorOptions are not nested inside NjordOptions
No options type SHALL be a property of another options type. `NjordOptions` SHALL contain only the forecast scope: locations, default models, horizons, forecast days and the requested parameter set. `SensorOptions` SHALL be registered and validated by `Njord.SensorHub`'s setup container, not the host.

#### Scenario: NjordOptions has no Grpc or Sensors property
- **WHEN** `NjordOptions` is resolved from DI
- **THEN** it SHALL NOT contain properties for `Grpc`, `Sensors`, `Mqtt`, `Enrichment`, `PollInterval` or `BudgetOverride`

#### Scenario: SensorHubActor uses IOptions of SensorOptions
- **WHEN** `SensorHubActor` is constructed
- **THEN** it SHALL receive `IOptions<SensorOptions>` for sensor fallback configuration

## REMOVED Requirements

### Requirement: Sub-option validators are co-located with their options class, except Mqtt
**Reason**: The Mqtt exception existed only because Mqtt was nested in NjordOptions; with no nested options there is no exception left.
**Migration**: Each options type, including MqttOptions, has its own validator next to it (see the added requirement).

### Requirement: MqttOptions and EnrichmentOptions stay nested in NjordOptions, dual-bound
**Reason**: The dual binding existed because the admin API mutated and persisted `NjordOptions` as one unit. Section-scoped writing (`writable-options`) removes that need, and the dual binding created two sources of truth.
**Migration**: Consumers inject `IOptions<MqttOptions>` or the specific enrichment options type. The admin API writes the affected section through the section writer.

## ADDED Requirements

### Requirement: Every options type has exactly one co-located validator
Every options type SHALL have exactly one `IValidateOptions<T>` validator for that type, defined next to the options class and registered by the same setup extension that binds it. No validator SHALL validate a different options type than the one it is registered for.

#### Scenario: Consensus validator validates ConsensusOptions
- **WHEN** invalid consensus settings are provided (e.g., quorum < 1)
- **THEN** the consensus validator SHALL fail startup validation of `ConsensusOptions`

#### Scenario: Mqtt validator validates MqttOptions
- **WHEN** `Njord:Mqtt:Enabled` is `true` and `Njord:Mqtt:Host` is missing
- **THEN** the MQTT validator SHALL fail startup validation of `MqttOptions` naming the missing host
