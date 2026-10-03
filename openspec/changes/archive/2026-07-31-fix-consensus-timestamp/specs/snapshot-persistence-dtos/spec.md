## MODIFIED Requirements

### Requirement: Enrichment snapshot DTOs use discriminated wrapper
The enrichment persistence layer SHALL define a DTO that wraps each enrichment result with a `TypeName` string discriminator and a serialized `JsonPayload`. On save, the concrete enrichment type name and its JSON representation SHALL be stored. On recovery, `TypeName` SHALL select the deserialization target.

The `EnrichmentTypes` dictionary SHALL contain entries for ALL enrichment result types: `AlertResult`, `IndexResult`, `TrendResult`, `DerivedResult`, `EnergyResult`, `ConsensusResult`, and `HistoryResult`. Missing entries cause silent data loss on snapshot recovery.

`ConsensusResult.ComputedAt` SHALL be included in the serialized `JsonPayload` via a `[JsonProperty("computedAt")]` attribute. When deserializing a `ConsensusResult` from a pre-upgrade snapshot where `computedAt` is absent, `ComputedAt` SHALL default to `null`. Callers SHALL treat null as "unknown computation time" and fall back to wall-clock time.

#### Scenario: ConsensusResult with ComputedAt round-trips through DTO
- **WHEN** a `ConsensusResult` with `ComputedAt = 2026-07-31T06:00:00Z` is stored via `EnrichmentSnapshotMapping.ToDto` and recovered via `ToDomain`
- **THEN** the recovered `ConsensusResult.ComputedAt` SHALL be `2026-07-31T06:00:00Z`

#### Scenario: Legacy ConsensusResult without ComputedAt recovers as null
- **WHEN** a persisted `EnrichmentEntryDto.JsonPayload` for a `ConsensusResult` does not contain a `computedAt` field
- **THEN** the recovered `ConsensusResult.ComputedAt` SHALL be `null`

#### Scenario: AlertResult round-trips through DTO
- **WHEN** an AlertResult is stored and the actor restarts
- **THEN** the AlertResult is recovered with identical data

#### Scenario: IndexResult round-trips through DTO
- **WHEN** an IndexResult is stored and the actor restarts
- **THEN** the IndexResult is recovered with identical data

#### Scenario: HistoryResult round-trips through DTO
- **WHEN** a HistoryResult is stored and the actor restarts
- **THEN** the HistoryResult is recovered with identical data

#### Scenario: Unknown type name on recovery is dropped
- **WHEN** a snapshot contains a TypeName not in the EnrichmentTypes dictionary
- **THEN** that entry is silently dropped during recovery
