## MODIFIED Requirements

### Requirement: IStatelessEnrichment defines consensus-in events-out computation

`IStatelessEnrichment.Compute` SHALL accept a single `ConsensusSnapshot` parameter (which includes the location) and return `IEnumerable<EgressEvent>`.

#### Scenario: Stateless enrichment produces events from ConsensusSnapshot
- **WHEN** a stateless enrichment's `Compute` is called with a `ConsensusSnapshot`
- **THEN** it produces `EgressEvent` instances using consensus data from `ConsensusSnapshot.Location`

### Requirement: IStatefulEnrichment defines diff-based computation

`IStatefulEnrichment.Compute` SHALL accept a `ConsensusSnapshot` and a nullable `ConsensusSnapshot?` previous parameter.

#### Scenario: First snapshot produces no output
- **WHEN** `Compute` is called with `previous` as null
- **THEN** no events are produced

#### Scenario: Subsequent snapshot produces trend events
- **WHEN** `Compute` is called with both current and previous `ConsensusSnapshot`
- **THEN** trend events are produced comparing the two

### Requirement: Features are registered via DI

All enrichment features SHALL be registered as `IEnrichmentFeature` singletons via DI. Consensus SHALL NOT be registered as an `IEnrichmentFeature` — it is a pipeline stage, not an enrichment.

#### Scenario: 6 enrichment features are discoverable
- **WHEN** `IEnumerable<IEnrichmentFeature>` is resolved from the DI container
- **THEN** exactly 6 features are returned: alerts, derived, trends, indices, energy, history

#### Scenario: Consensus is not in the feature registry
- **WHEN** `IEnumerable<IEnrichmentFeature>` is resolved
- **THEN** no feature with `TypeName` "consensus" SHALL be present

#### Scenario: Feature receives its dependencies via DI
- **WHEN** an enrichment feature is constructed
- **THEN** it receives `IOptions`, `TimeProvider`, and other dependencies via constructor injection

## REMOVED Requirements

### Requirement: IStatelessEnrichment defines snapshot-in events-out computation
**Reason**: Replaced by new `IStatelessEnrichment` that accepts `ConsensusSnapshot` instead of `ModelSnapshot`.
**Migration**: Change `Compute(ModelSnapshot snapshot, IReadOnlyList<string> locations)` to `Compute(ConsensusSnapshot consensus)`.
