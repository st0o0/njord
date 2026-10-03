## MODIFIED Requirements

### Requirement: GetEnrichments returns latest enrichment snapshot
`ForecastService.GetEnrichments` SHALL query the `EnrichmentSnapshotActor` via Ask to retrieve the latest enrichment results for a location. It SHALL map domain Result types to proto messages via `EnrichmentProtoMapper`. The `EnrichmentSnapshotStore` class is removed.

#### Scenario: Enrichments queried via actor Ask
- **WHEN** a client calls `GetEnrichments` with location "lucerne"
- **THEN** the service SHALL Ask `EnrichmentSnapshotActor` for all enrichments for that location and map them to the proto response

### Requirement: EnrichmentSnapshotStore captures latest results
**This requirement is REMOVED.** The `EnrichmentSnapshotStore` class and `EnrichmentSnapshotConsumerActor` are replaced by `EnrichmentSnapshotActor` (Akka Persistence).
