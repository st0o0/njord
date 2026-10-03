## MODIFIED Requirements

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

### Requirement: DiscoveryContext bundles common discovery parameters
The system SHALL define a `DiscoveryContext` record in `Njord.Mqtt` with fields `Mqtt` (MqttOptions), `PollInterval` (TimeSpan) and `Version` (string). All presenter `BuildDiscoveryPayload` calls SHALL receive a `DiscoveryContext` and the location instead of individual parameters.

#### Scenario: DiscoveryContext replaces parameter threading
- **WHEN** `BuildDiscoveryPayload` is called on any presenter
- **THEN** it SHALL receive a `DiscoveryContext` and the location — not separate `mqtt`, `pollInterval`, `version` parameters

### Requirement: Device envelope helper eliminates boilerplate
The system SHALL provide a `BuildDeviceEnvelope(string deviceId, string location,
string typeLabel, string version, JsonObject components)` helper method in `Njord.Mqtt`. All
presenter `BuildDiscoveryPayload` implementations SHALL use this helper instead of
duplicating the device JSON structure.

#### Scenario: Device envelope is structurally identical across presenters
- **WHEN** two different presenters build discovery payloads for the same location
- **THEN** the outer device envelope structure (`dev`, `o`, `qos`, `cmps`) SHALL
  be identical — only `cmps` content differs

### Requirement: IActorEnrichment defines actor-driven computation
The system SHALL define `IActorEnrichment : IEnrichmentFeature` in `Njord.Enrichment` with a method `CreateFlow(IUntypedActorContext context)` returning `Flow<ModelSnapshot, EgressEvent, NotUsed>`. The feature SHALL own the construction of its stream stage including creation of child actors through the given context; the enrichment actor SHALL connect the returned flow between the model-snapshot source and the egress sink.

#### Scenario: History builds its own stream stage
- **WHEN** `HistoryEnrichment.CreateFlow` is called
- **THEN** it SHALL resolve one `ForecastHistoryActor` child per configured location through the given context and return a flow that records each `ModelSnapshot` with those actors and emits `EgressEvent.EnrichmentUpdate` events with `TypeName` `"history"`

#### Scenario: History does not block the stream thread
- **WHEN** History queries its child actors for history state
- **THEN** it SHALL use `SelectAsync` with an async lambda, not `.Result`

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
