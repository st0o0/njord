## MODIFIED Requirements

### Requirement: Features are registered per endpoint module via DI
All enrichment feature implementations SHALL be registered as `IEnrichmentFeature` singletons in the DI container, scoped to their endpoint module. Each `IEndpointModule` SHALL declare which enrichment features it owns. The module's `BuildSubGraph()` SHALL wire only its own enrichments. There SHALL be no global `IEnumerable<IEnrichmentFeature>` that mixes enrichments from different endpoints.

#### Scenario: Weather module owns weather-specific enrichments
- **WHEN** `WeatherModule` is resolved from DI
- **THEN** it SHALL contain enrichment features [consensus, alerts, derived, trends, indices, energy, history] — the same 7 as before

#### Scenario: Future AQ module owns AQ-specific enrichments
- **WHEN** an `AirQualityModule` is registered (future change)
- **THEN** it SHALL contain its own enrichment features (e.g., AQI index) that are separate from weather enrichments

#### Scenario: Enrichments are still independently testable
- **WHEN** unit-testing `ConsensusEnrichment`
- **THEN** it SHALL be constructable via DI with its own dependencies — module scoping does not prevent independent instantiation

#### Scenario: Feature receives its dependencies via DI
- **WHEN** `ConsensusEnrichment` is constructed
- **THEN** it SHALL receive `ResolvedParameterSet`, horizons, `TimeProvider`, and `ConsensusOptions` via constructor — not via `Compute` parameters

### Requirement: IEnrichmentFeature defines the base contract
The system SHALL define an `IEnrichmentFeature` interface with properties `TypeName` (string), `Enabled` (bool), and methods `DeviceId(string location)`, `BuildDiscoveryPayload(DiscoveryContext ctx, string location)`, and `ToStateMessages(object result, string baseTopic)`. This interface is unchanged — enrichment features continue to work the same way, they are just owned by modules instead of registered globally.

#### Scenario: Feature exposes its type name
- **WHEN** an `IEnrichmentFeature` instance is queried for `TypeName`
- **THEN** it SHALL return a stable kebab-case identifier (e.g. `"consensus"`, `"alerts"`)

#### Scenario: Feature reports enabled state from configuration
- **WHEN** `EnrichmentOptions.Consensus.Enabled` is `false`
- **THEN** the `ConsensusEnrichment.Enabled` property SHALL return `false`
