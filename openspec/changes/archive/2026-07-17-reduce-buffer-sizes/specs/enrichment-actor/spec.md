## MODIFIED Requirements

### Requirement: EnrichmentActor materializes a ModelSnapshot BroadcastHub
The `EnrichmentActor` SHALL materialize a `BroadcastHub.Sink<ModelSnapshot>` with a buffer size of 8 to distribute rolling snapshots to enrichment features. The `ModelSnapshot.Update()` method SHALL use `ImmutableDictionary` with structural sharing instead of cloning a mutable `Dictionary` on every update.

#### Scenario: ModelSnapshot BroadcastHub buffer size is 8
- **WHEN** the EnrichmentActor materializes its enrichment graph
- **THEN** the ModelSnapshot BroadcastHub SHALL use a buffer size of 8

#### Scenario: ModelSnapshot update uses structural sharing
- **WHEN** `ModelSnapshot.Update()` is called with a new forecast
- **THEN** it SHALL return a new `ModelSnapshot` using `ImmutableDictionary.SetItem` without copying the entire dictionary
