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

#### Scenario: AlertResult round-trips through DTO
- **WHEN** an `AlertResult` is saved as an enrichment snapshot and recovered
- **THEN** the recovered value SHALL be an `AlertResult` with the same data

#### Scenario: IndexResult round-trips through DTO
- **WHEN** an `IndexResult` is saved as an enrichment snapshot and recovered
- **THEN** the recovered value SHALL be an `IndexResult` with the same data

#### Scenario: Unknown type name on recovery is dropped
- **WHEN** an enrichment DTO contains a `TypeName` that does not match any known enrichment type
- **THEN** the entry SHALL be silently dropped during recovery

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

### Requirement: CLAUDE.md caveat is removed
The CLAUDE.md caveat about `EnrichmentEntryDto` inner-JSON limitation SHALL be removed once all enrichment result records are hardened.

#### Scenario: CLAUDE.md no longer warns about inner-JSON gap
- **WHEN** all enrichment result records have `[JsonProperty]` on every property
- **THEN** the caveat sentence in CLAUDE.md Conventions is removed
