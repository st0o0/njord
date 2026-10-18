## Why

"Snapshot" has a precise meaning in this codebase: the persisted state of a persistent actor (`SaveSnapshot`, `SnapshotOffer`, `Persisted<Name>State`). It is also used for eight unrelated types: `ModelSnapshot` (the latest forecast per location and model plus a changed flag), `ConsensusSnapshot` (one location's consensus result), `SensorSnapshot`/`SensorSnapshots` (aggregated sensor readings), `ConfigSnapshot` (the effective configuration), three `*HealthSnapshot` records and the `GetSnapshot()` mapping extensions. A reader cannot tell whether a type is persisted, and the name hides what the type is. FunkArr has no `Snapshot` types outside persistence. Separately, the new `Njord.Core.Configuration.ParameterSelection` collides with the older `Njord.Messages.Config.ParameterSelection` DTO, which already needed a using-alias and a fully qualified name.

## What Changes

- Rename the types so that no type name contains `Snapshot`:

  | Old | New |
  |---|---|
  | `ModelSnapshot` | `ModelForecasts` |
  | `ConsensusSnapshot` | `LocationConsensus` |
  | `SensorSnapshot` | `SensorReadings` |
  | `SensorSnapshots` | `LocationSensorReadings` |
  | `QuerySensorSnapshot`, `SensorSnapshotResponse`, `SensorSnapshotResult`, `SensorSnapshotNotAvailable` | `QuerySensorReadings`, `SensorReadingsResponse`, `SensorReadingsResult`, `SensorReadingsNotAvailable` |
  | `RequestSensorSnapshots` | `RequestSensorReadings` |
  | `ConfigSnapshot`, `ConfigSnapshotMappings` | `EffectiveConfig`, `EffectiveConfigMappings` |
  | `MqttHealthSnapshot`, `PollHealthSnapshot`, `BudgetHealthSnapshot` | `MqttHealth`, `PollHealth`, `BudgetHealth` |
  | `GetSnapshot()` on poll worker and budget state | `ToPollStateEntry()`, `ToBudgetUsage()` |
  | `Njord.Messages.Config.ParameterSelection` (DTO) | `ParameterScope` |

- Akka terms stay as they are (`SaveSnapshot`, `SnapshotOffer`, `SaveSnapshotSuccess`, `SnapshotInterval`) because they name the framework concept; `Persisted<Name>State` stays the snapshot type.
- Member and variable names that held the old words follow (`Snapshots` property, `GetSnapshot` helpers, test helper names).
- An architecture spec fails when a production type name contains `Snapshot`, with no allow-list.
- The 25 main specs, `AGENTS.md`, `docs/` and the Verify golden-master file names that mention the old names are updated; capability directory names (`consensus-snapshot`, `model-snapshot`) stay, their purpose text uses the new names.
- No wire change: proto message names, gRPC payloads, MQTT payloads, persistence JSON and metrics are unchanged.

## Capabilities

### New Capabilities
- `type-naming`: reserves "Snapshot" for persisted actor state and names the renamed contract types.

### Modified Capabilities
<!-- main specs are updated textually (names only) in the final task; no requirement behaviour changes -->

## Impact

- About 250 production and test files across Weather, Sensors, Compute, Messages, Core, Pipeline, SensorHub, Budget, Mqtt, Grpc, Alerts, Derived, Trends and Indices; Verify `.verified.*` file names that embed type names; docs and specs.
- Pure rename, byte-identical golden masters and wire formats are the proof. Not breaking; commit type `refactor:`.
- Applies last in the current series (after `persistence-mapping-and-enums` and `options-parity-with-funkarr`), because it touches the most files.
