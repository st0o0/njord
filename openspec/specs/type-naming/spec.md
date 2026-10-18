# type-naming Specification

## Purpose
TBD - created by archiving change snapshot-naming. Update Purpose after archive.
## Requirements
### Requirement: Snapshot is reserved for persisted actor state
No production type SHALL have `Snapshot` in its name. The persisted state of a persistent actor SHALL be named `Persisted<Name>State` and is the only thing treated as a snapshot; Akka framework members (`SaveSnapshot`, `SnapshotOffer`, `SaveSnapshotSuccess`, `SnapshotInterval`) keep their names.

#### Scenario: A type name contains Snapshot
- **WHEN** a production type named `FooSnapshot` is added
- **THEN** the architecture spec fails

#### Scenario: Persisted state keeps its name
- **WHEN** the persistence project is inspected
- **THEN** `PersistedPollWorkerState` and `PersistedBudgetTrackerState` are unchanged

### Requirement: Contract types are named for what they are
The types formerly named with `Snapshot` SHALL use these names: `ModelForecasts` (latest forecast per location and model), `LocationConsensus` (one location's consensus), `SensorReadings` and `LocationSensorReadings` with `QuerySensorReadings`, `SensorReadingsResponse`, `SensorReadingsResult`, `SensorReadingsNotAvailable` and `RequestSensorReadings`, `EffectiveConfig` and `EffectiveConfigMappings`, and `MqttHealth`, `PollHealth`, `BudgetHealth`. State-to-message conversions SHALL be `ToPollStateEntry()` and `ToBudgetUsage()`.

#### Scenario: Behaviour is unchanged by the rename
- **WHEN** the gRPC golden masters, the MQTT payload snapshots and the persistence wire-format specs are verified after the rename
- **THEN** their content is byte-identical (only file names that embed type names change)

### Requirement: Type names are unique across namespaces for contract types
A type name used in `Njord.Messages` SHALL NOT be reused by another production type in a different namespace. The configuration DTO formerly named `ParameterSelection` SHALL be named `ParameterScope`, so `ParameterSelection` names only the resolver in `Njord.Core.Configuration`.

#### Scenario: No alias needed
- **WHEN** a file uses both the resolver and the configuration DTO
- **THEN** it needs no using-alias or fully qualified name to tell them apart

