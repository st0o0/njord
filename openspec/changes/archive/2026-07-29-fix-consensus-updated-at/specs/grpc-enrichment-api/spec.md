## MODIFIED Requirements

### Requirement: GetEnrichments returns latest enrichment snapshot
`ForecastService.GetEnrichments` SHALL query the `EnrichmentSnapshotActor` via Ask to retrieve the latest enrichment results for a location. It SHALL map domain Result types to proto messages via `EnrichmentProtoMapper`. When the snapshot includes a consensus result, the response SHALL also carry `consensus_updated_at`, the timestamp at which that snapshot was assembled, so callers can resolve horizon-offset fields (e.g. `HorizonConsensus.horizon = "h0"`) against a real point in time instead of guessing one.

#### Scenario: Enrichments queried via actor Ask
- **WHEN** a client calls `GetEnrichments` with location "lucerne"
- **THEN** the service SHALL Ask `EnrichmentSnapshotActor` for all enrichments for that location and map them to the proto response

#### Scenario: No data yet returns empty enrichments
- **WHEN** a client calls `GetEnrichments` before any enrichment computation has completed
- **THEN** the response SHALL return with empty/default enrichment fields (not an error)

#### Scenario: Unknown location returns NOT_FOUND
- **WHEN** a client calls `GetEnrichments` with an unconfigured location
- **THEN** the RPC SHALL return gRPC status `NOT_FOUND`

#### Scenario: Consensus payload carries a reference timestamp
- **WHEN** a client calls `GetEnrichments` and the snapshot includes a consensus result
- **THEN** the response SHALL set `consensus_updated_at` to the server time at which the snapshot was assembled, using the same timestamp already used to map that consensus result for `StreamEnrichments`

#### Scenario: No consensus result omits the timestamp
- **WHEN** a client calls `GetEnrichments` and the snapshot has no consensus result yet
- **THEN** `consensus_updated_at` SHALL be left unset rather than defaulting to an arbitrary value
