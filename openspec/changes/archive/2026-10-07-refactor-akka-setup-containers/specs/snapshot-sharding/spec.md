## ADDED Requirements

### Requirement: Akka.Streams fan-out for cross-shard forecast queries
The system SHALL use an Akka.Streams fan-out pattern for `QueryAllForecasts` across the `IForecastSnapshotRegion` ShardRegion. The fan-out SHALL use `Source.From(knownKeys).Ask<T>(region, timeout, parallelism)` with `ResumingDecider` supervision and `PipeTo` for actor-safe result delivery. The known entity keys SHALL be derived from configured locations and models.

#### Scenario: Fan-out queries all known forecast entities
- **WHEN** `QueryAllForecasts` is requested
- **THEN** the system SHALL fan out `QueryForecast` messages to all known `(location, modelId)` entity keys via the ShardRegion

#### Scenario: Failed entity does not abort the fan-out
- **WHEN** one entity times out or fails during fan-out
- **THEN** the `ResumingDecider` SHALL skip that entity and the result SHALL contain the remaining successful responses

#### Scenario: Fan-out parallelism is bounded
- **WHEN** the fan-out executes
- **THEN** the number of concurrent Ask operations SHALL be bounded (not unbounded)

### Requirement: Akka.Streams fan-out for cross-shard enrichment queries
The system SHALL use the same Akka.Streams fan-out pattern for `QueryAllEnrichments(location)` across the `IEnrichmentSnapshotRegion` ShardRegion. The known entity keys SHALL be derived from the location and the registered enrichment type names.

#### Scenario: Fan-out queries all enrichment types for a location
- **WHEN** `QueryAllEnrichments("lucerne")` is requested
- **THEN** the system SHALL fan out `QueryEnrichment` messages to all known `(lucerne, typeName)` entity keys

#### Scenario: Failed enrichment entity does not abort the fan-out
- **WHEN** one enrichment entity times out during fan-out
- **THEN** the result SHALL contain the remaining successful responses
