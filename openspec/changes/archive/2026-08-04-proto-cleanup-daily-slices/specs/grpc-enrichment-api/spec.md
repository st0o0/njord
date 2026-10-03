## MODIFIED Requirements

### Requirement: Proto messages map all enrichment domain types

The `EnrichmentProtoMapper.MapIndices` method SHALL accept an `IndexResult` (with `Days` list) and return an `IndexUpdate` with `repeated DayScoreSet days`. For each `DayScoreSet` in the domain result, the mapper SHALL create a proto `DayScoreSet` with all 8 scores, `hours_included`, and `ScoreEnvelope` fields (when non-null). `FrostProtection` SHALL be mapped to `FrostInfo` on the `IndexUpdate`. `Vpd` SHALL be mapped to `VpdInfo` on the `IndexUpdate`.

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

### Requirement: StreamEnrichments pushes enrichment updates in real-time

`ForecastService.StreamEnrichments` SHALL be a server-streaming RPC. It SHALL subscribe to the EgressActor BroadcastHub, filter for `EnrichmentUpdate` events, map them to typed proto messages via the enrichment feature's type name, and write them to the gRPC response stream. For consensus events, the `updated_at` field in the `EnrichmentEvent` proto SHALL use `EnrichmentUpdate.UpdatedAt` (the computation timestamp). For non-consensus events where `UpdatedAt` is null, it SHALL fall back to `timeProvider.GetUtcNow()`.

#### Scenario: Index update carries daily slices

- **WHEN** an index enrichment result arrives with 3 day score sets
- **THEN** the `IndexUpdate` SHALL contain 3 `DayScoreSet` entries with scores, envelopes, frost, and VPD
