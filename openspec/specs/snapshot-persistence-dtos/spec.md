# snapshot-persistence-dtos Specification

## Purpose

Serialization-safe DTO types for Akka Persistence snapshots. Replaces direct persistence of domain objects with dedicated DTOs that use only primitives, arrays, and string-keyed dictionaries, ensuring stable serialization across code changes.

## Requirements

### Requirement: Forecast snapshot DTOs use only serialization-safe types
The forecast persistence layer SHALL define DTO types that represent snapshot state using only arrays, string-keyed dictionaries, and primitive types. `ForecastSnapshotDto` SHALL contain a `Dictionary<string, ModelForecastDto>`. `ModelForecastDto` SHALL contain `ForecastPointDto[]` for hourly and `DailyForecastPointDto[]` for daily series. `ForecastPointDto` SHALL use `Dictionary<string, double?>` keyed by `ParameterDef.ApiName`. `DailyForecastPointDto` SHALL store `DateOnly` as ISO string.

#### Scenario: ForecastPointDto uses string keys
- **WHEN** a `ForecastPoint` with `ParameterDef(ApiName: "temperature_2m")` -> 28.8 is mapped to a DTO
- **THEN** the DTO SHALL contain `Values["temperature_2m"] = 28.8`

#### Scenario: ForecastSeries maps to array
- **WHEN** a `ForecastSeries` with 96 points is mapped to a DTO
- **THEN** the DTO SHALL contain a `ForecastPointDto[]` of length 96

#### Scenario: DailyForecastPoint date stored as ISO string
- **WHEN** a `DailyForecastPoint` with `Date = 2026-07-15` is mapped to a DTO
- **THEN** the DTO SHALL contain `Date = "2026-07-15"`

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

### Requirement: DTO mapping handles missing parameters gracefully
When recovering a `ForecastPointDto` whose `Values` dictionary contains a key not found in `ParameterRegistry`, that entry SHALL be silently dropped. The remaining parameters SHALL be mapped normally.

#### Scenario: Removed parameter is dropped on recovery
- **WHEN** a persisted DTO contains `Values["removed_param"] = 5.0` and `ParameterRegistry.GetByApiName("removed_param")` returns null
- **THEN** the recovered `ForecastPoint.Values` SHALL NOT contain that parameter

### Requirement: Enrichment result inner JSON wire names are pinned
All enrichment result records serialized inside `EnrichmentEntryDto.JsonPayload` SHALL have `[JsonProperty]` attributes on every property, producing stable camelCase wire names. Value tuples SHALL be replaced with named records carrying `[JsonProperty]` attributes.

#### Scenario: Enrichment result round-trips through nested JSON with stable wire names
- **WHEN** an enrichment result (e.g., `IndexResult`) is serialized via `EnrichmentSnapshotMapping.ToDto` and deserialized via `EnrichmentSnapshotMapping.ToDomain`
- **THEN** all property values round-trip correctly and the JSON wire format matches the Verify-approved snapshot

#### Scenario: Unknown fields in nested JSON are ignored on deserialization
- **WHEN** a persisted `EnrichmentEntryDto.JsonPayload` contains JSON fields not present in the current record definition
- **THEN** deserialization succeeds and the unknown fields are silently ignored

### Requirement: Scheduler snapshot DTO preserves poll state
The persistence layer SHALL define `SchedulerSnapshotDto` and `ModelPollStateDto` types that represent the SchedulerActor's full `_states` dictionary. `ModelPollStateDto` SHALL contain: `LastHash` (int?), `LastChangeUtcTicks` (long?), `PrevChangeUtcTicks` (long?), `NextPollUtcTicks` (long), `MissCount` (int), `CycleTicks` (long?), `Phase` (string). All `DateTimeOffset` values SHALL be stored as UTC ticks. All properties SHALL have `[JsonProperty]` attributes with stable wire names.

#### Scenario: Full state dictionary round-trips through DTO
- **WHEN** the SchedulerActor saves a snapshot with multiple model states
- **THEN** all states are recovered with identical values after restart

#### Scenario: Empty state dictionary produces valid snapshot
- **WHEN** the SchedulerActor saves a snapshot with no model states
- **THEN** recovery produces an empty state dictionary

### Requirement: CLAUDE.md caveat is removed
The CLAUDE.md caveat about `EnrichmentEntryDto` inner-JSON limitation SHALL be removed once all enrichment result records are hardened.

#### Scenario: CLAUDE.md no longer warns about inner-JSON gap
- **WHEN** all enrichment result records have `[JsonProperty]` on every property
- **THEN** the caveat sentence in CLAUDE.md Conventions is removed
