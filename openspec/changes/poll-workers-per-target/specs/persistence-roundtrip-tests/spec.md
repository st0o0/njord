## MODIFIED Requirements

### Requirement: Every persistence record has a journal-serializer roundtrip test
For every persistence record in `Njord.Persistence`, a test in `Njord.Persistence.Tests` SHALL serialize the record via `Newtonsoft.Json` with `TypeNameHandling.All` (through `PersistenceRoundtripHelper.Roundtrip`), deserialize it back, and assert field-level equality.

#### Scenario: PersistedBudgetTrackerState roundtrip
- **WHEN** a `PersistedBudgetTrackerState` with representative field values is serialized and deserialized
- **THEN** all fields have identical values after the roundtrip

#### Scenario: ApiCallRecorded roundtrip
- **WHEN** an `ApiCallRecorded` with representative field values is serialized and deserialized
- **THEN** all fields have identical values after the roundtrip

#### Scenario: PersistedPollSchedulerState roundtrip
- **WHEN** a `PersistedPollWorkerState` (the poll worker snapshot that replaces the removed scheduler snapshot) with every field populated is serialized and deserialized
- **THEN** all fields have identical values after the roundtrip

#### Scenario: PersistedDataChanged roundtrip
- **WHEN** each poll worker event (`PollingStarted`, `PollingStopped`, `ForecastChanged`, `ForecastUnchanged`, `PollFailed`, `PollDeferred`), which replace the removed `PersistedDataChanged`, is serialized and deserialized
- **THEN** all fields have identical values after the roundtrip

#### Scenario: Poll worker records have wire-format snapshots
- **WHEN** the poll worker snapshot and events are serialized with indented Newtonsoft JSON
- **THEN** the output matches its approved Verify baseline
