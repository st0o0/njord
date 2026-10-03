## ADDED Requirements

### Requirement: IEndpointModule defines the module contract
The system SHALL define an `IEndpointModule` interface with:
- `EndpointType EndpointType` — the module's endpoint identifier
- `CreateTargets(LocationOptions location, CycleId cycleId) → IReadOnlyList<WeightedTarget>` — produces typed targets for this endpoint given a location and cycle
- `Flow<WeightedTarget, EgressEvent> BuildSubGraph()` — returns a self-contained Akka.Streams flow that casts the base target to the concrete type, fetches, builds snapshots, runs enrichments, and emits `EgressEvent`s
- `IReadOnlyList<DeviceInfo> GetDeviceDefinitions(LocationOptions location)` — returns HA device definitions for MQTT discovery

#### Scenario: Weather module creates one target per resolved model
- **WHEN** `WeatherModule.CreateTargets(lucerne, cycle1)` is called and lucerne resolves to 3 models
- **THEN** it SHALL return 3 `WeatherTarget` instances, one per model, each with the correct `Weight`

#### Scenario: Module without models creates one target per location
- **WHEN** an endpoint module that has no model dimension (e.g., AirQuality) creates targets for lucerne
- **THEN** it SHALL return exactly 1 target for that location

#### Scenario: BuildSubGraph returns a complete isolated flow
- **WHEN** `WeatherModule.BuildSubGraph()` is called
- **THEN** the returned `Flow<WeightedTarget, EgressEvent>` SHALL internally cast to `WeatherTarget`, call `IWeatherClient.FetchAsync`, aggregate into snapshots, run weather-specific enrichments, and map results to `EgressEvent`

#### Scenario: Device definitions include enrichment devices
- **WHEN** `WeatherModule.GetDeviceDefinitions(lucerne)` is called and consensus + alerts enrichments are enabled
- **THEN** it SHALL return device definitions for each model device plus one device per enabled enrichment feature

### Requirement: Endpoint modules are registered via DI
All `IEndpointModule` implementations SHALL be registered as singletons in the DI container. The `PipelineActor` SHALL receive `IReadOnlyList<IEndpointModule>` via constructor injection and use only enabled modules when building the partition graph.

#### Scenario: Only enabled modules are wired into the pipeline
- **WHEN** Weather is enabled and AirQuality is disabled in configuration
- **THEN** `IReadOnlyList<IEndpointModule>` SHALL contain only the `WeatherModule` and the partition SHALL have 1 outlet

#### Scenario: Module receives its dependencies via DI
- **WHEN** `WeatherModule` is constructed
- **THEN** it SHALL receive `IWeatherClient`, weather enrichment features, and configuration via constructor injection

### Requirement: Each module composes client, snapshot builder, and enrichments
An `IEndpointModule` implementation SHALL compose its sub-graph from injectable parts: a typed client (e.g., `IWeatherClient`), a snapshot builder, and endpoint-specific enrichment features. The module acts as composition root — the parts are independently testable.

#### Scenario: Client is injectable and testable in isolation
- **WHEN** unit-testing `WeatherClient`
- **THEN** it SHALL be constructable with a mock `HttpClient` without requiring the full module

#### Scenario: Enrichments are independently injectable
- **WHEN** unit-testing `ConsensusEnrichment`
- **THEN** it SHALL be constructable via DI without requiring the `WeatherModule`
