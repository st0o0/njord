# enrichment-feature-registry Specification

## Purpose

Type system and registry for enrichment features. Defines the `IEnrichmentFeature` hierarchy (stateless, stateful, actor-driven), discovery context, DI registration, device envelope helper, and parameterised topic scheme methods that replace type-specific wiring throughout the enrichment and egress layers.

## Requirements

### Requirement: IEnrichmentFeature defines the base contract
The system SHALL define an `IEnrichmentFeature` interface with properties
`TypeName` (string), `Enabled` (bool), and methods `DeviceId(string location)`,
`BuildDiscoveryPayload(DiscoveryContext ctx, string location)`, and
`ToStateMessages(object result, string baseTopic)`.

#### Scenario: Feature exposes its type name
- **WHEN** an `IEnrichmentFeature` instance is queried for `TypeName`
- **THEN** it SHALL return a stable kebab-case identifier (e.g. `"consensus"`,
  `"alerts"`)

#### Scenario: Feature reports enabled state from configuration
- **WHEN** `EnrichmentOptions.Consensus.Enabled` is `false`
- **THEN** the `ConsensusEnrichment.Enabled` property SHALL return `false`

### Requirement: IStatelessEnrichment defines consensus-in events-out computation

`IStatelessEnrichment.Compute` SHALL accept a `ConsensusSnapshot` and an optional `SensorSnapshot?` parameter (which includes the location) and return `IEnumerable<EgressEvent>`. Implementations that do not use sensor data SHALL ignore the parameter.

#### Scenario: Stateless enrichment produces events from ConsensusSnapshot
- **WHEN** a stateless enrichment's `Compute` is called with a `ConsensusSnapshot`
- **THEN** it produces `EgressEvent` instances using consensus data from `ConsensusSnapshot.Location`

#### Scenario: Compute called with sensor data
- **WHEN** the enrichment pipeline runs with available sensor readings
- **THEN** `Compute` SHALL be called with a non-null `SensorSnapshot`

#### Scenario: Compute called without sensor data
- **WHEN** the enrichment pipeline runs without any sensor readings
- **THEN** `Compute` SHALL be called with a null `SensorSnapshot`

### Requirement: IStatefulEnrichment defines diff-based computation

`IStatefulEnrichment.Compute` SHALL accept a `ConsensusSnapshot`, a nullable `ConsensusSnapshot?` previous parameter, and an optional `SensorSnapshot?` parameter. Implementations that do not use sensor data SHALL ignore the parameter.

#### Scenario: First snapshot produces no output
- **WHEN** `Compute` is called with `previous` as null
- **THEN** no events are produced

#### Scenario: Subsequent snapshot produces trend events
- **WHEN** `Compute` is called with both current and previous `ConsensusSnapshot`
- **THEN** trend events are produced comparing the two

#### Scenario: Compute called with sensor data
- **WHEN** the enrichment pipeline runs with available sensor readings
- **THEN** `Compute` SHALL be called with a non-null `SensorSnapshot`

#### Scenario: Compute called without sensor data
- **WHEN** the enrichment pipeline runs without any sensor readings
- **THEN** `Compute` SHALL be called with a null `SensorSnapshot`

### Requirement: IActorEnrichment defines actor-driven computation
The system SHALL define `IActorEnrichment : IEnrichmentFeature` with a method
`Materialize(Source<ModelSnapshot, NotUsed> source,
Sink<EgressEvent, NotUsed> sink, IMaterializer mat,
IUntypedActorContext context)`. The feature SHALL own the full stream
materialisation including child actor creation.

#### Scenario: History materialises its own stream graph
- **WHEN** `HistoryEnrichment.Materialize` is called
- **THEN** it SHALL create per-location child `ForecastHistoryActor` instances,
  wire a `SelectAsync`-based stream graph, and connect source to sink

#### Scenario: History does not block the stream thread
- **WHEN** History queries its child actors for history state
- **THEN** it SHALL use `SelectAsync` with an async lambda, not `.Result`

### Requirement: DiscoveryContext bundles common discovery parameters
The system SHALL define a `DiscoveryContext` record with fields `Location`
(string), `Mqtt` (MqttOptions), `PollInterval` (TimeSpan), and `Version`
(string). All `BuildDiscoveryPayload` calls SHALL receive a `DiscoveryContext`
instead of individual parameters.

#### Scenario: DiscoveryContext replaces parameter threading
- **WHEN** `BuildDiscoveryPayload` is called on any feature
- **THEN** it SHALL receive a `DiscoveryContext` — not separate `mqtt`,
  `pollInterval`, `version` parameters

### Requirement: Features are registered via DI

All enrichment features SHALL be registered as `IEnrichmentFeature` singletons via DI. Consensus SHALL NOT be registered as an `IEnrichmentFeature` — it is a pipeline stage, not an enrichment.

#### Scenario: 6 enrichment features are discoverable
- **WHEN** `IEnumerable<IEnrichmentFeature>` is resolved from the DI container
- **THEN** exactly 5 features are returned: alerts, derived, trends, indices, history

#### Scenario: Consensus is not in the feature registry
- **WHEN** `IEnumerable<IEnrichmentFeature>` is resolved
- **THEN** no feature with `TypeName` "consensus" SHALL be present

#### Scenario: Feature receives its dependencies via DI
- **WHEN** an enrichment feature is constructed
- **THEN** it receives `IOptions`, `TimeProvider`, and other dependencies via constructor injection

### Requirement: Device envelope helper eliminates boilerplate
The system SHALL provide a `BuildDeviceEnvelope(string deviceId, string location,
string typeLabel, string version, JsonObject components)` helper method. All
`BuildDiscoveryPayload` implementations SHALL use this helper instead of
duplicating the device JSON structure.

#### Scenario: Device envelope is structurally identical across features
- **WHEN** two different features build discovery payloads for the same location
- **THEN** the outer device envelope structure (`dev`, `o`, `qos`, `cmps`) SHALL
  be identical — only `cmps` content differs

### Requirement: TopicScheme provides parameterised enrichment methods
The system SHALL provide `EnrichmentDeviceId(string location, string typeName)`
and `EnrichmentTopic(string baseTopic, string location, string typeName)` methods
that replace the 14 type-specific methods. Per-model `DeviceId` and `ConfigTopic`
SHALL remain unchanged.

#### Scenario: Device ID follows consistent pattern
- **WHEN** `EnrichmentDeviceId("lucerne", "consensus")` is called
- **THEN** it SHALL return `"njord_lucerne_consensus"`

#### Scenario: Topic follows consistent pattern
- **WHEN** `EnrichmentTopic("njord", "lucerne", "consensus")` is called
- **THEN** it SHALL return `"njord/lucerne/consensus"`
