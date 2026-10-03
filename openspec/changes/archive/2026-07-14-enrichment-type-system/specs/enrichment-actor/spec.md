## MODIFIED Requirements

### Requirement: Consumer streams are materialized only when enabled
The `EnrichmentActor` SHALL iterate over all `IEnrichmentFeature` instances
received via DI. For each feature where `Enabled` is `true`, the actor SHALL
materialise the appropriate consumer stream based on the feature's interface
type:
- `IStatelessEnrichment<T>`: `SelectMany(snapshot => feature.Compute(snapshot, locations))`
- `IStatefulEnrichment<T>`: `Scan` to pair current/previous, then `SelectMany(pair => feature.Compute(...))`
- `IActorEnrichment`: delegate to `feature.Materialize(source, sink, mat, context)`

For features where `Enabled` is `false`, no consumer stream SHALL be
materialised.

#### Scenario: Disabled feature is not materialized
- **WHEN** an `IEnrichmentFeature` has `Enabled` set to `false`
- **THEN** no consumer stream is materialised for that feature

#### Scenario: Enabled stateless feature is materialized via loop
- **WHEN** an `IStatelessEnrichment<T>` has `Enabled` set to `true`
- **THEN** a consumer stream is materialised using
  `SelectMany(snapshot => feature.Compute(snapshot, locations))`

#### Scenario: Enabled stateful feature uses Scan pairing
- **WHEN** an `IStatefulEnrichment<T>` has `Enabled` set to `true`
- **THEN** a consumer stream is materialised with a `Scan` operator carrying
  the previous snapshot and calling `feature.Compute(snapshot, previous, locations)`

#### Scenario: Enabled actor feature delegates materialisation
- **WHEN** an `IActorEnrichment` has `Enabled` set to `true`
- **THEN** the actor calls `feature.Materialize(source, sink, mat, context)`
  and does not wire the stream itself

### Requirement: EnrichmentActor fans out enrichment results to EgressActor

Each enrichment consumer sub-graph SHALL produce `EgressEvent.EnrichmentUpdate`
instances (carrying `Location`, `TypeName`, and `Result`) and send them to the
EgressActor's MergeHub via `ISinkRef<EgressEvent>`. The `EnrichmentActor` SHALL
NOT contain type-specific Materialize methods — all dispatch is via the feature
registry.

#### Scenario: Enrichment produces EnrichmentUpdate
- **WHEN** any enrichment feature computes a result for location "lucerne"
- **THEN** it SHALL emit `EgressEvent.EnrichmentUpdate("lucerne", feature.TypeName, result)`
  into the EgressActor's MergeHub

#### Scenario: No type-specific Materialize methods
- **WHEN** the `EnrichmentActor` source file is inspected
- **THEN** it SHALL NOT contain methods named `MaterializeConsensusConsumer`,
  `MaterializeAlertConsumer`, `MaterializeDerivedConsumer`,
  `MaterializeTrendConsumer`, `MaterializeIndexConsumer`,
  `MaterializeEnergyConsumer`, or `MaterializeHistoryConsumer`

### Requirement: The EnrichmentActor materializes a history consumer stream when enabled
The `EnrichmentActor` SHALL delegate history stream materialisation to the
`IActorEnrichment.Materialize` method. The history feature SHALL create
per-location child `ForecastHistoryActor` instances as children of the
`EnrichmentActor`. The stream SHALL use `SelectAsync` for actor queries, not
blocking `.Result`.

#### Scenario: History uses SelectAsync
- **WHEN** the history enrichment queries a `ForecastHistoryActor`
- **THEN** it SHALL use `SelectAsync` with an async lambda

#### Scenario: History actors are children of EnrichmentActor
- **WHEN** the history consumer is enabled with 2 locations
- **THEN** 2 ForecastHistoryActor children exist, one per location

### Requirement: ForecastHistoryActor uses TimeProvider
The `ForecastHistoryActor` SHALL receive `TimeProvider` via constructor
injection and use `timeProvider.GetUtcNow()` for all time operations. It
SHALL NOT use `DateTimeOffset.UtcNow` directly.

#### Scenario: TimeProvider is injected
- **WHEN** `ForecastHistoryActor` computes a retention cutoff
- **THEN** it SHALL use `timeProvider.GetUtcNow()` as the reference time
