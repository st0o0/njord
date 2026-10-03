# enrichment-actor Delta Specification (admin-ui)

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

The `locations` list passed to `Compute` methods SHALL be a live reference
obtained from `IOptionsMonitor<NjordOptions>.CurrentValue.Locations` at
invocation time, not a list captured once during graph materialization.

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

#### Scenario: Locations reflect runtime config changes
- **WHEN** a new location "berlin" is added via the admin UI and a snapshot arrives
- **THEN** the `locations` list passed to `Compute` includes "berlin"

#### Scenario: Removed location excluded from enrichment
- **WHEN** location "borken" is removed via the admin UI and a snapshot arrives
- **THEN** the `locations` list passed to `Compute` does not include "borken"
