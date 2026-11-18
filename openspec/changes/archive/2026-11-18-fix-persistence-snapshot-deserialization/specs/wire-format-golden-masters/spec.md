## ADDED Requirements

### Requirement: Wire-format tests cover mapper-produced snapshots
Wire-format roundtrip tests SHALL include a path that exercises the real persistence mapper (e.g. `GetPersistenceState()` for PollWorker) in addition to hand-constructed DTOs. This ensures the concrete collection types produced by the mapper survive Newtonsoft serialization.

#### Scenario: Mapper-produced PollWorker snapshot roundtrips through Newtonsoft
- **WHEN** a `PollWorkerState` with populated domain states is mapped via `GetPersistenceState()`
- **THEN** the resulting `PersistedPollWorkerState` roundtrips through `JsonConvert.SerializeObject` / `DeserializeObject` with `TypeNameHandling.All` without error
- **AND** the deserialized `DomainStates` collection has the same count and values as the original
