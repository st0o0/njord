# persistence-roundtrip-tests Specification

## Purpose

Verify-snapshot "shape" tests on persistence DTOs catch structural drift but not deserialization bugs (e.g. a property that serializes fine but round-trips to the wrong value). Roundtrip tests close that gap.

## Requirements

### Requirement: Every persistence DTO has a roundtrip test
For every persistence DTO class in `Njord.Persistence`, a roundtrip test SHALL serialize the DTO via `Newtonsoft.Json` with `TypeNameHandling.All` (the Akka.Persistence.Sql serializer setting), deserialize it back, and assert field-level equality.

#### Scenario: BudgetTrackerSnapshotDto roundtrip
- **WHEN** a `BudgetTrackerSnapshotDto` with representative field values is serialized and deserialized
- **THEN** all fields SHALL have identical values after the roundtrip

#### Scenario: ApiCallRecordedDto roundtrip
- **WHEN** an `ApiCallRecordedDto` with representative field values is serialized and deserialized
- **THEN** all fields SHALL have identical values after the roundtrip

#### Scenario: SchedulerSnapshotDto roundtrip
- **WHEN** a `SchedulerSnapshotDto` with populated model poll states is serialized and deserialized
- **THEN** all fields including nested `ModelPollStateDto` entries SHALL have identical values

#### Scenario: ForecastSnapshotDto roundtrip
- **WHEN** a `ForecastSnapshotDto` with hourly and daily forecast points is serialized and deserialized
- **THEN** all fields including nested collections SHALL have identical values

#### Scenario: EnrichmentSnapshotDto roundtrip
- **WHEN** an `EnrichmentSnapshotDto` with enrichment entries is serialized and deserialized
- **THEN** all fields including nested `EnrichmentEntryDto` entries SHALL have identical values

#### Scenario: ForecastHistorySnapshotDto roundtrip
- **WHEN** a `ForecastHistorySnapshotDto` with forecast records is serialized and deserialized
- **THEN** all fields including nested `ForecastRecordDto` entries SHALL have identical values

### Requirement: Roundtrip tests use the same serializer settings as production
The roundtrip tests SHALL use `JsonSerializerSettings` with `TypeNameHandling.All` and the default `DefaultContractResolver`, matching the Akka.Persistence.Sql serialization behavior.

#### Scenario: Type metadata preserved in roundtrip
- **WHEN** a DTO is serialized with `TypeNameHandling.All`
- **THEN** the `$type` property SHALL be present in the JSON and the deserializer SHALL resolve it to the correct CLR type
