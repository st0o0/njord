## REMOVED Requirements

### Requirement: IEnrichmentFeature defines the base contract
**Reason**: The `IEnrichmentFeature` hierarchy is replaced by standalone feature
libraries with typed input interfaces. No common feature interface; no
string-based type names.
**Migration**: Each feature becomes an independent library.

### Requirement: IStatelessEnrichment defines consensus-in events-out computation
**Reason**: Removed with `IEnrichmentFeature` hierarchy. Stateless features are
now standalone analyzers with typed input interfaces.
**Migration**: Each stateless feature becomes its own library.

### Requirement: IStatefulEnrichment defines diff-based computation
**Reason**: Removed with `IEnrichmentFeature` hierarchy. Stateful features now
receive previous state via their typed input interface.
**Migration**: `TrendAnalyzer` receives `ITrendInput` with `Previous` property.

### Requirement: IActorEnrichment defines actor-driven computation
**Reason**: Removed with `IEnrichmentFeature` hierarchy. Actor-driven features
are standalone libraries containing their own actors.
**Migration**: `ForecastHistoryActor` moved to `Njord.History` library.

### Requirement: DiscoveryContext bundles common discovery parameters
**Reason**: Discovery is removed entirely.
**Migration**: None — discovery capability is dropped.

### Requirement: Features are registered via DI
**Reason**: Multi-registration of `IEnumerable<IEnrichmentFeature>` with runtime
dispatch is eliminated. Each feature is registered by its own DI extension
method as a concrete type.
**Migration**: Replace with per-feature `services.AddAlerts()`, etc.

### Requirement: Device envelope helper eliminates boilerplate
**Reason**: Discovery is removed; no discovery payloads are built.
**Migration**: None — discovery capability is dropped.

### Requirement: TopicScheme provides parameterised enrichment methods
**Reason**: Enrichment-specific topic helpers tied to `EnrichmentDeviceId` and
`EnrichmentTypeNames` are removed with the string-based type system.
**Migration**: The MQTT consumer defines its own topic structure.
