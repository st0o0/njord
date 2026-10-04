## ADDED Requirements

### Requirement: Feature libraries register their own options
Each feature library's `ServiceCollectionExtensions` SHALL register its own options via `AddOptions<T>().Bind().ValidateOnStart()`. The host's `NjordServiceSetup` SHALL NOT register sub-options that belong to feature libraries.

#### Scenario: AddNjordMqtt registers MqttOptions and its validator
- **WHEN** `services.AddNjordMqtt()` is called with a configuration section
- **THEN** `MqttOptions` SHALL be bound to `"Njord:Mqtt"` and validated on startup

#### Scenario: NjordServiceSetup delegates sub-option registration
- **WHEN** `NjordServiceSetup.SetupServices` executes
- **THEN** it SHALL call `AddNjordMqtt()`, `AddNjordGrpc()`, `AddNjordEnrichment()`, `AddNjordPipeline()`, `AddNjordIngest()` which each register their own sub-options

### Requirement: NjordOptions contains only cross-cutting configuration
`NjordOptions` SHALL contain only properties that span multiple features: `Locations`, `Models`, `Horizons`, `PollIntervalMinutes`, `OpenMeteoBaseUrl`, `Parameters`, `PersistencePath`.

#### Scenario: NjordOptions has no nested sub-option types
- **WHEN** `NjordOptions` is inspected
- **THEN** it SHALL NOT have properties of type `MqttOptions`, `GrpcOptions`, `SensorOptions`, `EnrichmentOptions`, or `PersistenceOptions`
