# grpc-enrichment-api Specification

## Purpose

gRPC RPCs for querying and streaming enrichment data (alerts, indices, trends,
derived values, history, consensus). Backed by the
`EnrichmentSnapshotActor` (Akka Persistence) queried via Ask.

## Requirements

### Requirement: GetEnrichments returns latest enrichment snapshot
`ForecastService.GetEnrichments` SHALL query the `EnrichmentSnapshotActor` via Ask to retrieve the latest enrichment results for a location. It SHALL map domain Result types to proto messages via `EnrichmentProtoMapper`. When the snapshot includes a consensus result, the response SHALL set `consensus_updated_at` to the consensus computation timestamp (`ConsensusResult.ComputedAt`), NOT the current wall-clock time. If `ComputedAt` is null (legacy snapshot recovery), the service SHALL fall back to `timeProvider.GetUtcNow()`.

#### Scenario: Enrichments queried via actor Ask
- **WHEN** a client calls `GetEnrichments` with location "lucerne"
- **THEN** the service SHALL Ask `EnrichmentSnapshotActor` for all enrichments for that location and map them to the proto response

#### Scenario: No data yet returns empty enrichments
- **WHEN** a client calls `GetEnrichments` before any enrichment computation has completed
- **THEN** the response SHALL return with empty/default enrichment fields (not an error)

#### Scenario: Unknown location returns NOT_FOUND
- **WHEN** a client calls `GetEnrichments` with an unconfigured location
- **THEN** the RPC SHALL return gRPC status `NOT_FOUND`

#### Scenario: Consensus timestamp reflects computation time, not query time
- **WHEN** consensus was computed at 06:00 UTC and a client calls `GetEnrichments` at 12:00 UTC
- **THEN** `consensus_updated_at` SHALL be `2026-07-31T06:00:00Z`, not `2026-07-31T12:00:00Z`

#### Scenario: Legacy snapshot without ComputedAt falls back to wall clock
- **WHEN** the `EnrichmentSnapshotActor` recovers a consensus result from a pre-upgrade snapshot (where `ComputedAt` is null) and a client calls `GetEnrichments`
- **THEN** `consensus_updated_at` SHALL fall back to `timeProvider.GetUtcNow()`

#### Scenario: No consensus result omits the timestamp
- **WHEN** a client calls `GetEnrichments` and the snapshot has no consensus result yet
- **THEN** `consensus_updated_at` SHALL be left unset rather than defaulting to an arbitrary value

### Requirement: StreamEnrichments pushes enrichment updates in real-time
`ForecastService.StreamEnrichments` SHALL be a server-streaming RPC. It SHALL subscribe to the EgressActor BroadcastHub, filter for `EnrichmentUpdate` events, map them to typed proto messages via the enrichment feature's type name, and write them to the gRPC response stream. For consensus events, the `updated_at` field in the `EnrichmentEvent` proto SHALL use `EnrichmentUpdate.UpdatedAt` (the computation timestamp). For non-consensus events where `UpdatedAt` is null, it SHALL fall back to `timeProvider.GetUtcNow()`.

#### Scenario: Alert update pushed to client
- **WHEN** the alert enrichment computes a new result for location "lucerne"
- **THEN** all `StreamEnrichments` clients SHALL receive an `EnrichmentEvent` with type "alerts" and an `AlertUpdate` payload containing severity and confidence per alert type

#### Scenario: Consensus update carries computation timestamp
- **WHEN** a consensus result computed at 06:15 UTC flows through the stream at 06:15:02 UTC
- **THEN** the `EnrichmentEvent.updated_at` SHALL be `06:15:00Z` (the computation time), not `06:15:02Z`

#### Scenario: Non-consensus update uses wall-clock time
- **WHEN** an alert result flows through the stream at 06:15:02 UTC
- **THEN** the `EnrichmentEvent.updated_at` SHALL be `06:15:02Z` (wall-clock time, as `UpdatedAt` is null)

#### Scenario: Index update carries daily slices
- **WHEN** an index enrichment result arrives with 3 day score sets
- **THEN** the `IndexUpdate` SHALL contain 3 `DayScoreSet` entries with scores, envelopes, frost, and VPD

### Requirement: Proto messages map all enrichment domain types

The `EnrichmentProtoMapper.MapIndices` method SHALL accept an `IndexResult` (with `Days` list) and return an `IndexUpdate` with `repeated DayScoreSet days`. For each `DayScoreSet` in the domain result, the mapper SHALL create a proto `DayScoreSet` with all 8 scores, `hours_included`, and `ScoreEnvelope` fields (when non-null). `FrostProtection` SHALL be mapped to `FrostInfo` on the `IndexUpdate`. `Vpd` SHALL be mapped to `VpdInfo` on the `IndexUpdate`.

The `ConsensusUpdate` proto message SHALL carry hourly and daily parameter consensus. The `daily_summaries` field (which mapped `DailyConsensusSummary`) SHALL be deprecated. Daily consensus SHALL be represented as `ParameterConsensus` entries in a `daily_parameters` repeated field with `dN` horizon keys.

#### Scenario: MapIndices produces per-day entries
- **WHEN** `MapIndices` is called with an `IndexResult` containing 3 day score sets
- **THEN** the returned `IndexUpdate.Days` SHALL contain 3 `DayScoreSet` entries

#### Scenario: MapIndices maps envelopes
- **WHEN** a `DayScoreSet` has a non-null `OutdoorEnvelope` with min=65, max=80, confidence=0.9
- **THEN** the proto `DayScoreSet.outdoor_envelope` SHALL have `min=65`, `max=80`, `confidence=0.9`

#### Scenario: MapIndices maps frost info
- **WHEN** `IndexResult.FrostProtection` is `FrostProtectionInfo(14, 0.75)`
- **THEN** `IndexUpdate.frost` SHALL have `hours_until_frost=14`, `confidence=0.75`

#### Scenario: MapIndices omits frost when null
- **WHEN** `IndexResult.FrostProtection` is null
- **THEN** `IndexUpdate.frost` SHALL not be set

#### Scenario: MapIndices maps VPD
- **WHEN** `IndexResult.Vpd` is `VpdInfo("high", 1.27)`
- **THEN** `IndexUpdate.vpd` SHALL have `category="high"`, `kpa=1.27`

#### Scenario: AlertUpdate carries 9 alert types
- **WHEN** an alert update is mapped to proto
- **THEN** all 9 alert types are represented

#### Scenario: TrendUpdate carries parameter trends and timing
- **WHEN** a trend update is mapped to proto
- **THEN** parameter trends, precipitation timing, and extrema timing are included

#### Scenario: ConsensusUpdate carries per-parameter per-horizon data
- **WHEN** a `ConsensusSnapshot` is mapped to `ConsensusUpdate`
- **THEN** `parameters` contains hourly `ParameterConsensus` with `hN` horizon keys and `daily_parameters` contains daily `ParameterConsensus` with `dN` horizon keys

#### Scenario: ConsensusUpdate no longer carries daily summaries
- **WHEN** a `ConsensusSnapshot` is mapped to `ConsensusUpdate`
- **THEN** the `daily_summaries` field SHALL be empty (deprecated)
