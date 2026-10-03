# enrichment-feature-registry Specification

## Purpose

Type system and registry for enrichment features. Defines the `IEnrichmentFeature` hierarchy (stateless, stateful, actor-driven), discovery context, DI registration, device envelope helper, and parameterised topic scheme methods that replace type-specific wiring throughout the enrichment and egress layers.

## Requirements

### Requirement: IEnrichmentFeature defines the base contract
The system SHALL define an `IEnrichmentFeature` interface in `Njord.Enrichment` with exactly the properties `TypeName` (string) and `Enabled` (bool). It SHALL NOT expose device ids, discovery payloads or state messages; those belong to the `IEnrichmentPresenter` of the same `TypeName` in `Njord.Mqtt`.

#### Scenario: Feature exposes its type name
- **WHEN** an `IEnrichmentFeature` instance is queried for `TypeName`
- **THEN** it SHALL return a stable kebab-case identifier (e.g. `"alerts"`, `"indices"`)

#### Scenario: Feature reports enabled state from configuration
- **WHEN** `EnrichmentOptions.Alerts.Enabled` is `false`
- **THEN** the `AlertEnrichment.Enabled` property SHALL return `false`

#### Scenario: Feature has no MQTT surface
- **WHEN** the public members of `IEnrichmentFeature` and its derived interfaces are inspected
- **THEN** none of them reference `Njord.Mqtt` types or return topic, discovery or payload strings

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
The system SHALL define `IActorEnrichment : IEnrichmentFeature` in `Njord.Enrichment` with a method `CreateFlow(IUntypedActorContext context)` returning `Flow<ModelSnapshot, EgressEvent, NotUsed>`. The feature SHALL own the construction of its stream stage including creation of child actors through the given context; the enrichment actor SHALL connect the returned flow between the model-snapshot source and the egress sink.

#### Scenario: History builds its own stream stage
- **WHEN** `HistoryEnrichment.CreateFlow` is called
- **THEN** it SHALL resolve one `ForecastHistoryActor` child per configured location through the given context and return a flow that records each `ModelSnapshot` with those actors and emits `EgressEvent.EnrichmentUpdate` events with `TypeName` `"history"`

#### Scenario: History does not block the stream thread
- **WHEN** History queries its child actors for history state
- **THEN** it SHALL use `SelectAsync` with an async lambda, not `.Result`

### Requirement: DiscoveryContext bundles common discovery parameters
The system SHALL define a `DiscoveryContext` record in `Njord.Mqtt` with fields `Mqtt` (MqttOptions), `PollInterval` (TimeSpan) and `Version` (string). All presenter `BuildDiscoveryPayload` calls SHALL receive a `DiscoveryContext` and the location instead of individual parameters.

#### Scenario: DiscoveryContext replaces parameter threading
- **WHEN** `BuildDiscoveryPayload` is called on any presenter
- **THEN** it SHALL receive a `DiscoveryContext` and the location — not separate `mqtt`, `pollInterval`, `version` parameters

### Requirement: Features are registered via DI
All enrichment features SHALL be registered as `IEnrichmentFeature` singletons via DI by the `Njord.Enrichment` service registration. Consensus SHALL NOT be registered as an `IEnrichmentFeature` — it is a pipeline stage, not an enrichment. Presenters are not part of this registry; they are registered separately by `Njord.Mqtt` (see capability `mqtt-enrichment-presentation`).

#### Scenario: 5 enrichment features are discoverable
- **WHEN** `IEnumerable<IEnrichmentFeature>` is resolved from the DI container
- **THEN** exactly 5 features are returned: alerts, derived, trends, indices, history

#### Scenario: Consensus is not in the feature registry
- **WHEN** `IEnumerable<IEnrichmentFeature>` is resolved
- **THEN** no feature with `TypeName` "consensus" SHALL be present

#### Scenario: Feature receives its dependencies via DI
- **WHEN** an enrichment feature is constructed
- **THEN** it receives `IOptions`, `TimeProvider`, and other dependencies via constructor injection

#### Scenario: The feature library has no MQTT registration
- **WHEN** the service registration of `Njord.Enrichment` is inspected
- **THEN** it registers no `IEnrichmentPresenter` and references no `Njord.Mqtt` type

### Requirement: Device envelope helper eliminates boilerplate
The system SHALL provide a `BuildDeviceEnvelope(string deviceId, string location,
string typeLabel, string version, JsonObject components)` helper method in `Njord.Mqtt`. All
presenter `BuildDiscoveryPayload` implementations SHALL use this helper instead of
duplicating the device JSON structure.

#### Scenario: Device envelope is structurally identical across presenters
- **WHEN** two different presenters build discovery payloads for the same location
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
