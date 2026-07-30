# grpc-enrichment-api Specification

## Purpose

gRPC RPCs for querying and streaming enrichment data (alerts, indices, trends,
energy, derived values, history, consensus). Backed by the
`EnrichmentSnapshotActor` (Akka Persistence) queried via Ask.

## Requirements

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

### Requirement: Proto messages map all enrichment domain types
The proto definition SHALL include messages for all 7 enrichment types with full fidelity to the domain Result records. `EnrichmentProtoMapper.MapConsensus` SHALL map both the hourly `Parameters` and the `DailySummaries` from `ConsensusResult` to the proto `ConsensusUpdate` (fields `parameters` and `daily` respectively).

#### Scenario: AlertUpdate carries 9 alert types
- **WHEN** an `AlertUpdate` is serialized
- **THEN** it SHALL contain up to 9 alerts each with type (enum), severity (enum), and confidence (double)

#### Scenario: TrendUpdate carries parameter trends and timing
- **WHEN** a `TrendUpdate` is serialized
- **THEN** it SHALL contain parameter trends (direction + delta), precipitation timing (starts/ends in hours), extrema timing, stability, and decay rate

#### Scenario: ConsensusUpdate carries per-parameter per-horizon data
- **WHEN** a `ConsensusUpdate` is serialized
- **THEN** it SHALL contain per-parameter entries, each with per-horizon consensus values (median, spread, agreement, available model count)

#### Scenario: ConsensusUpdate carries daily summaries
- **WHEN** a `ConsensusUpdate` is serialized and the domain `ConsensusResult` contains `DailySummaries`
- **THEN** the proto `ConsensusUpdate.daily` SHALL contain one `DailyConsensus` entry per calendar day with temperature_max, temperature_min, precipitation_sum, wind_speed_max, weather_code, spread, agreement, and available_models
