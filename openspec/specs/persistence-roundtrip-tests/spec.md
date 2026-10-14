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

#### Scenario: PersistedPollWorkerState roundtrip
- **WHEN** a `PersistedPollWorkerState` with every field populated is serialized and deserialized
- **THEN** all fields have identical values after the roundtrip

#### Scenario: Poll worker events roundtrip
- **WHEN** each poll worker event (`PollingStarted`, `PollingStopped`, `ForecastChanged`, `ForecastUnchanged`, `PollFailed`, `PollDeferred`) is serialized and deserialized
- **THEN** all fields have identical values after the roundtrip

#### Scenario: Poll worker records have wire-format snapshots
- **WHEN** the poll worker snapshot and events are serialized with indented Newtonsoft JSON
- **THEN** the output matches its approved Verify baseline

