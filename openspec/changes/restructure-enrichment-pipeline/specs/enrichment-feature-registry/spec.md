## REMOVED Requirements

### Requirement: IEnrichmentFeature base contract
**Reason**: The `IEnrichmentFeature` hierarchy (`TypeName` + `Enabled`,
`IStatelessEnrichment`, `IStatefulEnrichment`, `IActorEnrichment`) is replaced
by standalone feature libraries with typed input interfaces. No common feature
interface; no string-based type names.
**Migration**: Each feature becomes an independent library with its own compute
class and input interface.

### Requirement: Features registered via DI as IEnumerable
**Reason**: Multi-registration of `IEnumerable<IEnrichmentFeature>` with
runtime `OfType<T>()` dispatch is eliminated. Each feature is registered by
its own DI extension method as a concrete type.
**Migration**: Replace `services.AddSingleton<IEnrichmentFeature, XxxEnrichment>()`
with per-feature `services.AddAlerts()`, `services.AddTrends()`, etc.

### Requirement: DiscoveryContext bundles common discovery parameters
**Reason**: `DiscoveryContext` existed to pass MQTT options, poll interval, and
version to presenters for discovery payload construction. With no discovery and
no presenters, this type is removed.
**Migration**: None — discovery capability is dropped.

### Requirement: Device envelope helper
**Reason**: `BuildDeviceEnvelope` was a shared method for constructing discovery
device envelopes. Removed with discovery.
**Migration**: None — discovery capability is dropped.

### Requirement: TopicScheme provides parameterised enrichment methods
**Reason**: `EnrichmentDeviceId` and `EnrichmentTopic` relied on
`EnrichmentTypeNames` string constants. Removed with the string-based registry.
**Migration**: The MQTT consumer defines topic generation from typed result
metadata.
