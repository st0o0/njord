# persistence-roundtrip-tests Specification

## Purpose

Verify-snapshot "shape" tests on persistence DTOs catch structural drift but not deserialization bugs (e.g. a property that serializes fine but round-trips to the wrong value). Roundtrip tests close that gap.
## Requirements
### Requirement: Roundtrip tests use the same serializer settings as production
The roundtrip tests SHALL use `JsonSerializerSettings` with `TypeNameHandling.All` and the default `DefaultContractResolver`, matching the Akka.Persistence.Sql serialization behavior.

#### Scenario: Type metadata preserved in roundtrip
- **WHEN** a DTO is serialized with `TypeNameHandling.All`
- **THEN** the `$type` property SHALL be present in the JSON and the deserializer SHALL resolve it to the correct CLR type

### Requirement: Every persistence record has a journal-serializer roundtrip test
For every persistence record in `Njord.Persistence`, a test in `Njord.Persistence.Tests` SHALL serialize the record via `Newtonsoft.Json` with `TypeNameHandling.All` (through `PersistenceRoundtripHelper.Roundtrip`), deserialize it back, and assert field-level equality.

#### Scenario: PersistedBudgetTrackerState roundtrip
- **WHEN** a `PersistedBudgetTrackerState` with representative field values is serialized and deserialized
- **THEN** all fields have identical values after the roundtrip

#### Scenario: ApiCallRecorded roundtrip
- **WHEN** an `ApiCallRecorded` with representative field values is serialized and deserialized
- **THEN** all fields have identical values after the roundtrip

#### Scenario: PersistedPollSchedulerState roundtrip
- **WHEN** a `PersistedPollSchedulerState` with populated `PersistedModelPollEntry` entries is serialized and deserialized
- **THEN** all fields including the nested entries have identical values

#### Scenario: PersistedDataChanged roundtrip
- **WHEN** a `PersistedDataChanged` is serialized and deserialized
- **THEN** all fields have identical values after the roundtrip

