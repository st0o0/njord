## ADDED Requirements

### Requirement: GetEnrichments reads the latest analysis from ForecastReadModel
`WeatherService.GetEnrichments` SHALL first resolve the location through `ConfigManager`'s catalog (`QueryCatalog`) and then ask `ForecastReadModel` with `QueryLatestAnalysis(LocationId)` through `ActorAsk`. It SHALL map the reply with `LatestAnalysisResponse.ToProto()` from `Njord.Grpc/Mapping/AnalysisProtoMappings.cs`. When the mapped analysis is a consensus result, the response SHALL set `consensus_updated_at` to the timestamp the pipeline stamped on `PipelineEvent.ConsensusComputed` when it computed the consensus, not the time of the query.

#### Scenario: Enrichments queried via actor Ask
- **WHEN** a client calls `GetEnrichments` with location "lucerne"
- **THEN** the service asks `ForecastReadModel` with `QueryLatestAnalysis(lucerne)` and maps the reply to the proto response

#### Scenario: No data yet returns empty enrichments
- **WHEN** a client calls `GetEnrichments` before any analysis has been computed
- **THEN** `ForecastReadModel` answers `LatestAnalysisNotAvailable` and the response carries only the location, with empty enrichment fields (not an error)

#### Scenario: Unknown location returns NOT_FOUND
- **WHEN** a client calls `GetEnrichments` with an unconfigured location
- **THEN** the RPC returns gRPC status `NOT_FOUND`

#### Scenario: Consensus timestamp reflects computation time, not query time
- **WHEN** consensus was computed at 06:00 UTC and a client calls `GetEnrichments` at 12:00 UTC
- **THEN** `consensus_updated_at` is `06:00:00Z`, not `12:00:00Z`

#### Scenario: No consensus result omits the timestamp
- **WHEN** a client calls `GetEnrichments` and no consensus has been computed for the location
- **THEN** `consensus_updated_at` is left unset

### Requirement: StreamEnrichments pushes analysis events in real time
`WeatherService.StreamEnrichments` SHALL be a server-streaming RPC. It SHALL request a `SourceRef<PipelineEvent>` from `ForecastReadModel` (`RequestReadModelStream`), keep the `ConsensusComputed`, `AlertComputed`, `DerivedComputed`, `TrendComputed` and `IndexComputed` events, map each with `ToEnrichmentEvent()` and write it to the response stream. `type_name` SHALL be the event's `Kind.ToWireName()` and `updated_at` SHALL be the event's `Timestamp` (the time the pipeline computed it) for every event type. A client cancellation SHALL shut down that call's stream through a shared kill switch.

#### Scenario: Alert update pushed to client
- **WHEN** the pipeline computes a new alert result for location "lucerne"
- **THEN** every `StreamEnrichments` client receives an `EnrichmentEvent` with type "alerts" and an `AlertUpdate` payload containing severity and confidence per alert type

#### Scenario: Update carries computation timestamp
- **WHEN** a consensus result computed at 06:15:00 UTC is written to the stream at 06:15:02 UTC
- **THEN** `EnrichmentEvent.updated_at` is `06:15:00Z`

#### Scenario: Index update carries daily slices
- **WHEN** an index result arrives with 3 day score sets
- **THEN** the `IndexUpdate` contains 3 `DayScoreSet` entries with scores, envelopes, frost, and VPD

### Requirement: Analysis results map to proto messages
`Njord.Grpc/Mapping/AnalysisProtoMappings.cs` SHALL provide `ToProto()` extensions for `AlertResult`, `IndexResult`, `TrendResult`, `DerivedResult` and `ConsensusResult`. `IndexResult.ToProto()` SHALL return an `IndexUpdate` with one proto `DayScoreSet` per domain day carrying all 8 scores, `hours_included` and each `ScoreEnvelope` that is non-null; `FrostProtection` SHALL map to `FrostInfo` and `Vpd` to `VpdInfo`, each only when present. `ConsensusUpdate` SHALL carry hourly parameter consensus as `ParameterConsensus` entries in `hourly_parameters` and daily parameter consensus in `daily_parameters`, with `hN` and `dN` horizon keys respectively.

#### Scenario: Index mapping produces per-day entries
- **WHEN** an `IndexResult` with 3 day score sets is mapped
- **THEN** `IndexUpdate.days` contains 3 `DayScoreSet` entries

#### Scenario: Index mapping maps envelopes
- **WHEN** a `DayScoreSet` has a non-null `OutdoorEnvelope` with min=65, max=80, confidence=0.9
- **THEN** the proto `DayScoreSet.outdoor_envelope` has `min=65`, `max=80`, `confidence=0.9`

#### Scenario: Index mapping maps frost info
- **WHEN** `IndexResult.FrostProtection` is `FrostProtectionInfo(14, 0.75)`
- **THEN** `IndexUpdate.frost` has `hours_until_frost=14`, `confidence=0.75`

#### Scenario: Index mapping omits frost when null
- **WHEN** `IndexResult.FrostProtection` is null
- **THEN** `IndexUpdate.frost` is not set

#### Scenario: Index mapping maps VPD
- **WHEN** `IndexResult.Vpd` is `VpdInfo("high", 1.27)`
- **THEN** `IndexUpdate.vpd` has `category="high"`, `kpa=1.27`

#### Scenario: AlertUpdate carries all 14 alert types
- **WHEN** an alert result is mapped to proto
- **THEN** all 14 alert types are represented

#### Scenario: TrendUpdate carries parameter trends and timing
- **WHEN** a trend result is mapped to proto
- **THEN** parameter trends, precipitation timing, and extrema timing are included

#### Scenario: ConsensusUpdate carries per-parameter per-horizon data
- **WHEN** a consensus result is mapped to `ConsensusUpdate`
- **THEN** `hourly_parameters` contains hourly `ParameterConsensus` with `hN` horizon keys and `daily_parameters` contains daily `ParameterConsensus` with `dN` horizon keys

## REMOVED Requirements

### Requirement: GetEnrichments returns latest enrichment snapshot
**Reason**: The requirement named types, files or behavior that no longer exist; it is restated against the current code.
**Migration**: Superseded by "GetEnrichments reads the latest analysis from ForecastReadModel" in this capability.

### Requirement: StreamEnrichments pushes enrichment updates in real-time
**Reason**: The requirement named types, files or behavior that no longer exist; it is restated against the current code.
**Migration**: Superseded by "StreamEnrichments pushes analysis events in real time" in this capability.

### Requirement: Proto messages map the enrichment domain types
**Reason**: The requirement named types, files or behavior that no longer exist; it is restated against the current code.
**Migration**: Superseded by "Analysis results map to proto messages" in this capability.
