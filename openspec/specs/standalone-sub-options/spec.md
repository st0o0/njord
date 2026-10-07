# standalone-sub-options Specification

## Purpose

Lets configuration sub-sections that don't need to flow through the admin
mutation API bind, validate, and inject independently of `NjordOptions`,
instead of forcing every consumer to depend on the full monolithic options
object. `Mqtt` and `Enrichment` are the deliberate exception, kept nested
because the admin gRPC API mutates and persists `NjordOptions` as one unit.

## Requirements

### Requirement: Each sub-option has its own SectionName
`MqttOptions`, `GrpcOptions`, `SensorOptions`, `EnrichmentOptions`, and `PersistenceOptions` SHALL each be a standalone class with a `public const string SectionName` field (e.g., `"Njord:Mqtt"`).

#### Scenario: MqttOptions binds to Njord:Mqtt section
- **WHEN** configuration contains `Njord:Mqtt:Host = "broker.local"`
- **THEN** `IOptions<MqttOptions>.Value.Host` SHALL equal `"broker.local"`

### Requirement: GrpcOptions and SensorOptions are not nested inside NjordOptions
`GrpcOptions` and `SensorOptions` SHALL NOT be properties of `NjordOptions`. `SensorOptions` SHALL be registered and validated by `Njord.Sensors`'s setup container, not the host.

#### Scenario: NjordOptions has no Grpc or Sensors property
- **WHEN** `NjordOptions` is resolved from DI
- **THEN** it SHALL NOT contain properties for `Grpc` or `Sensors`

#### Scenario: SensorHubActor uses IOptions of SensorOptions
- **WHEN** `SensorHubActor` is constructed
- **THEN** it SHALL receive `IOptions<SensorOptions>` for sensor fallback configuration

### Requirement: MqttOptions and EnrichmentOptions stay nested in NjordOptions, dual-bound
`MqttOptions` and `EnrichmentOptions` SHALL remain properties of `NjordOptions` *and* SHALL also be independently bindable via their own `SectionName`, because `AdminGrpcService` reads, mutates, and persists the whole `NjordOptions` object as one unit (`IOptionsMonitor<NjordOptions>` + `ConfigPersistence.SaveAsync(NjordOptions)`) for every admin `Set*` RPC, including `SetEnrichment*`. Feature-local consumers (actors, enrichment features, validators) SHALL use the standalone binding; the admin mutation/persistence path SHALL keep using the nested one.

#### Scenario: AddNjordMqtt registers standalone MqttOptions
- **WHEN** `Njord.Mqtt`'s setup container runs
- **THEN** `MqttOptions` SHALL be resolvable from the service provider via `IOptions<MqttOptions>`, bound from the same `Njord:Mqtt` section as `NjordOptions.Mqtt`

#### Scenario: MqttConnectionActor uses IOptions of MqttOptions
- **WHEN** `MqttConnectionActor` is constructed
- **THEN** it SHALL receive `IOptions<MqttOptions>`, not `IOptions<NjordOptions>`

#### Scenario: AdminGrpcService still mutates NjordOptions.Enrichment
- **WHEN** `SetEnrichmentAlerts` or another `SetEnrichment*` admin RPC is handled
- **THEN** it SHALL clone, mutate, and persist `NjordOptions` (including its nested `Enrichment` property) as today — this is intentionally unchanged by this capability

### Requirement: Sub-option validators are co-located with their options class, except Mqtt
Each sub-option class that is fully extracted (not dual-bound) SHALL have its own `IValidateOptions<SubOptions>` validator registered alongside it, not in the host. `MqttOptions` validation SHALL remain on `NjordOptionsValidator` because `Mqtt` stays nested in `NjordOptions`.

#### Scenario: ConsensusOptionsValidator validates EnrichmentOptions
- **WHEN** invalid consensus settings are provided (e.g., quorum < 1)
- **THEN** the `ConsensusOptionsValidator` SHALL produce a validation failure on `EnrichmentOptions` (via its standalone binding), not only on `NjordOptions`

#### Scenario: NjordOptionsValidator still validates Mqtt
- **WHEN** `NjordOptions.Mqtt.Enabled` is `true` and `Mqtt.Host` is missing
- **THEN** `NjordOptionsValidator` SHALL fail startup validation naming the missing MQTT host
