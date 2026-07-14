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

### Requirement: IStatelessEnrichment defines snapshot-in events-out computation
The system SHALL define `IStatelessEnrichment<TResult> : IEnrichmentFeature`
with a method `Compute(ModelSnapshot snapshot, IReadOnlyList<string> locations)`
returning `IEnumerable<EgressEvent>`. The method SHALL iterate over locations,
compute the result per location, and wrap each in an `EgressEvent.EnrichmentUpdate`.

#### Scenario: Stateless enrichment produces one event per location
- **WHEN** `Compute` is called with a snapshot and 2 locations
- **THEN** it SHALL return 2 `EgressEvent.EnrichmentUpdate` instances, one per
  location

### Requirement: IStatefulEnrichment defines diff-based computation
The system SHALL define `IStatefulEnrichment<TResult> : IEnrichmentFeature`
with a method `Compute(ModelSnapshot snapshot, ModelSnapshot? previous,
IReadOnlyList<string> locations)` returning `IEnumerable<EgressEvent>`. When
`previous` is `null`, the method SHALL return an empty sequence.

#### Scenario: First snapshot produces no output
- **WHEN** `Compute` is called with `previous` as `null`
- **THEN** it SHALL return an empty sequence

#### Scenario: Subsequent snapshot produces trend events
- **WHEN** `Compute` is called with both `snapshot` and `previous` non-null
- **THEN** it SHALL return one `EgressEvent.EnrichmentUpdate` per location

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
All enrichment feature implementations SHALL be registered as
`IEnrichmentFeature` singletons in the DI container. Actors SHALL receive
`IEnumerable<IEnrichmentFeature>` via constructor injection.

#### Scenario: All 7 features are discoverable
- **WHEN** `IEnumerable<IEnrichmentFeature>` is resolved from the container
- **THEN** it SHALL contain 7 instances with unique `TypeName` values

#### Scenario: Feature receives its dependencies via DI
- **WHEN** `ConsensusEnrichment` is constructed
- **THEN** it SHALL receive `ResolvedParameterSet`, horizons, `TimeProvider`,
  and `ConsensusOptions` via constructor — not via `Compute` parameters

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
