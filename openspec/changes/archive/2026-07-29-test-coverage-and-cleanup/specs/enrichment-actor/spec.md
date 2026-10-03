## MODIFIED Requirements

### Requirement: The EnrichmentActor maintains a ModelSnapshot via Scan
The `EnrichmentActor` SHALL materialize a consumer on the pipeline's `SourceRef<FetchOutcome>` using a `Scan` operator to accumulate a `ModelSnapshot`. It SHALL import `FetchOutcome` from `Njord.Domain.Weather`, not from `Njord.Ingest`. The Enrichment zone SHALL NOT reference the Ingest namespace.

#### Scenario: Success updates the snapshot
- **WHEN** a `FetchOutcome.Success` for (lucerne, icon_d2) arrives
- **THEN** the snapshot is updated with that forecast and emitted downstream

#### Scenario: No Ingest namespace import in Enrichment
- **WHEN** the codebase is compiled
- **THEN** no file under Njord.Enrichment contains `using Njord.Ingest`
