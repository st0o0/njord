# wire-format-golden-masters Specification

## Purpose
Pins the externally visible output of njord (gRPC analysis and
forecast responses and persistence records) with snapshot tests, so internal refactorings can prove they changed
nothing a consumer sees.
## Requirements
### Requirement: No orphaned snapshot files
Every `*.verified.*` file in a test project SHALL belong to an existing snapshot test.

#### Scenario: Orphan detection
- **WHEN** the test projects are inspected
- **THEN** there is no `*.verified.*` file without a test method that produces it

### Requirement: Wire-format tests cover mapper-produced snapshots
Wire-format roundtrip tests SHALL include a path that exercises the real persistence mapper (e.g. `GetPersistenceState()` for PollWorker) in addition to hand-constructed DTOs. This ensures the concrete collection types produced by the mapper survive Newtonsoft serialization.

#### Scenario: Mapper-produced PollWorker snapshot roundtrips through Newtonsoft
- **WHEN** a `PollWorkerState` with populated domain states is mapped via `GetPersistenceState()`
- **THEN** the resulting `PersistedPollWorkerState` roundtrips through `JsonConvert.SerializeObject` / `DeserializeObject` with `TypeNameHandling.All` without error
- **AND** the deserialized `DomainStates` collection has the same count and values as the original

