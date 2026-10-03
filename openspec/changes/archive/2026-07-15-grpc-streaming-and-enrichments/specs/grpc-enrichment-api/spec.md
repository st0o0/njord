## ADDED Requirements

### Requirement: GetEnrichments returns latest enrichment snapshot
`ForecastService.GetEnrichments` SHALL be a unary RPC accepting a location. It SHALL return the latest enrichment data for that location from the `EnrichmentSnapshotStore`, including alerts, indices, trends, energy, derived, history, and consensus results.

#### Scenario: All enrichment types returned
- **WHEN** a client calls `GetEnrichments` with location "lucerne" and all enrichment features are enabled
- **THEN** the response SHALL contain alert, index, trend, energy, derived, history, and consensus data

#### Scenario: No data yet returns empty enrichments
- **WHEN** a client calls `GetEnrichments` before any enrichment computation has completed
- **THEN** the response SHALL return with empty/default enrichment fields (not an error)

#### Scenario: Unknown location returns NOT_FOUND
- **WHEN** a client calls `GetEnrichments` with an unconfigured location
- **THEN** the RPC SHALL return gRPC status `NOT_FOUND`

### Requirement: StreamEnrichments pushes enrichment updates in real-time
`ForecastService.StreamEnrichments` SHALL be a server-streaming RPC. It SHALL subscribe to the EgressActor BroadcastHub, filter for `EnrichmentUpdate` events, map them to typed proto messages via the enrichment feature's type name, and write them to the gRPC response stream.

#### Scenario: Alert update pushed to client
- **WHEN** the alert enrichment computes a new result for location "lucerne"
- **THEN** all `StreamEnrichments` clients SHALL receive an `EnrichmentEvent` with type "alerts" and an `AlertUpdate` payload containing severity and confidence per alert type

#### Scenario: Consensus update pushed to client
- **WHEN** the consensus enrichment computes a new result
- **THEN** clients SHALL receive an `EnrichmentEvent` with type "consensus" and a `ConsensusUpdate` payload with per-parameter per-horizon median, spread, and agreement values

#### Scenario: Index update carries all scores
- **WHEN** an index enrichment result arrives
- **THEN** the `IndexUpdate` SHALL contain laundry, outdoor, running, cycling, bbq, irrigation, solar, ventilation scores plus HDD, CDD, frost protection, and VPD

### Requirement: EnrichmentSnapshotStore captures latest results
`EnrichmentSnapshotStore` SHALL be a thread-safe singleton maintaining the latest enrichment result per (location, type) pair. It SHALL be updated from `EnrichmentUpdate` events on the BroadcastHub and queryable by the `GetEnrichments` RPC.

#### Scenario: Store is populated from EnrichmentUpdate
- **WHEN** an `EnrichmentUpdate` for ("lucerne", "alerts") arrives via the BroadcastHub
- **THEN** the store SHALL hold the latest `AlertResult` for that pair

#### Scenario: Store overwrites on new data
- **WHEN** a new `EnrichmentUpdate` for the same (location, type) arrives
- **THEN** the store SHALL replace the previous result

### Requirement: Proto messages map all enrichment domain types
The proto definition SHALL include messages for all 7 enrichment types with full fidelity to the domain Result records.

#### Scenario: AlertUpdate carries 9 alert types
- **WHEN** an `AlertUpdate` is serialized
- **THEN** it SHALL contain up to 9 alerts each with type (enum), severity (enum), and confidence (double)

#### Scenario: TrendUpdate carries parameter trends and timing
- **WHEN** a `TrendUpdate` is serialized
- **THEN** it SHALL contain parameter trends (direction + delta), precipitation timing (starts/ends in hours), extrema timing, stability, and decay rate

#### Scenario: ConsensusUpdate carries per-parameter per-horizon data
- **WHEN** a `ConsensusUpdate` is serialized
- **THEN** it SHALL contain per-parameter entries, each with per-horizon consensus values (median, spread, agreement, available model count)
