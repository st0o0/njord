## MODIFIED Requirements

### Requirement: Persistence mappers live outside the state types
Mapping between runtime state and persisted records SHALL be extension methods in `*PersistenceMappings` classes in the owning project. State types contain only state and transition logic. Collection properties in persistence records SHALL be materialized as `List<T>` (not collection expressions or other compiler-generated types) so that the serialized `$type` is a well-known type Newtonsoft.Json can construct.

#### Scenario: State roundtrip through persistence
- **WHEN** a `PollWorkerState` is mapped with `GetPersistenceState()` and back with `FromPersistence()`
- **THEN** the resulting state equals the original

#### Scenario: Serialized collection type is List
- **WHEN** a persistence mapper produces a snapshot containing a collection property
- **THEN** the runtime type of that collection SHALL be `List<T>`, not a compiler-generated type

### Requirement: Every persistence record has a roundtrip test
Every persistence record SHALL have a test that serializes and deserializes it using `JsonSerializerSettings` with `TypeNameHandling.All` (matching Akka.Persistence.Sql), verifying all fields survive. Roundtrip tests for snapshot records SHALL include a test that exercises the real persistence mapper output (e.g. `GetPersistenceState()`), not only hand-constructed DTOs.

#### Scenario: Roundtrip preserves all fields
- **WHEN** any persistence record is serialized and deserialized with `TypeNameHandling.All`
- **THEN** all fields have identical values after the roundtrip

#### Scenario: Mapper-produced snapshot roundtrips
- **WHEN** a snapshot DTO is produced by the real persistence mapper from a populated runtime state
- **THEN** serializing and deserializing it with `TypeNameHandling.All` preserves all fields
