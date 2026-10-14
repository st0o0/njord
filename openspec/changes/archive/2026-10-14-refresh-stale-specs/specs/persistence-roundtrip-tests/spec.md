## ADDED Requirements

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

## REMOVED Requirements

### Requirement: Every persistence DTO has a roundtrip test
**Reason**: The requirement named types, files or behavior that no longer exist; it is restated against the current code.
**Migration**: Superseded by "Every persistence record has a journal-serializer roundtrip test" in this capability.
