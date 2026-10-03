## MODIFIED Requirements

### Requirement: Enrichment snapshot DTOs use discriminated wrapper
The enrichment persistence layer SHALL define a DTO that wraps each enrichment result with a `TypeName` string discriminator and a serialized `JsonPayload`. On save, the concrete enrichment type name and its JSON representation SHALL be stored. On recovery, `TypeName` SHALL select the deserialization target.

The `EnrichmentTypes` dictionary SHALL contain entries for ALL enrichment result types: `AlertResult`, `IndexResult`, `TrendResult`, `DerivedResult`, `EnergyResult`, `ConsensusResult`, and `HistoryResult`. Missing entries cause silent data loss on snapshot recovery.

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

## ADDED Requirements

### Requirement: Scheduler snapshot DTO preserves poll state
The persistence layer SHALL define `SchedulerSnapshotDto` and `ModelPollStateDto` types that represent the SchedulerActor's full `_states` dictionary. `ModelPollStateDto` SHALL contain: `LastHash` (int?), `LastChangeUtcTicks` (long?), `PrevChangeUtcTicks` (long?), `NextPollUtcTicks` (long), `MissCount` (int), `CycleTicks` (long?), `Phase` (string). All `DateTimeOffset` values SHALL be stored as UTC ticks. All properties SHALL have `[JsonProperty]` attributes with stable wire names.

#### Scenario: Full state dictionary round-trips through DTO
- **WHEN** the SchedulerActor saves a snapshot with multiple model states
- **THEN** all states are recovered with identical values after restart

#### Scenario: Empty state dictionary produces valid snapshot
- **WHEN** the SchedulerActor saves a snapshot with no model states
- **THEN** recovery produces an empty state dictionary
