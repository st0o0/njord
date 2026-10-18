# config-persistence Specification

## Purpose

Atomic file-based persistence for runtime config mutations. User overrides are
stored in `data/appsettings.Override.json`, loaded as the last configuration
source (highest priority), and propagated to all subscribers via
`IConfigurationRoot.Reload()`. A corrupt or unreadable override file never
prevents startup, a corrupt reload keeps the last good values, and njord never
deletes or overwrites a corrupt file's content.
## Requirements
### Requirement: Config mutations persist to override file
All config mutations SHALL persist user overrides to `data/appsettings.Override.json`. The file SHALL be written atomically (write to temp file, then rename). The content SHALL be wrapped in a `{"Njord": {...}}` section to match the `NjordOptions.SectionName` configuration binding. On startup, njord SHALL load the override file as the last configuration source, giving it higher priority than environment variables. An override file that cannot be read or parsed (for example truncated JSON, an empty file, or a root that is not a JSON object) SHALL NOT prevent startup: njord SHALL log an Error naming the file and the problem, SHALL start with the remaining configuration layers (`appsettings*.json`, environment variables), and SHALL leave the file unchanged. A readable override file whose values fail option validation SHALL still fail startup.

#### Scenario: Mutation persists to file
- **WHEN** a config mutation succeeds
- **THEN** `data/appsettings.Override.json` SHALL be updated with the section-wrapped config state

#### Scenario: Config survives restart
- **WHEN** njord restarts after a config mutation
- **THEN** the mutated config SHALL be loaded from `data/appsettings.Override.json` and applied

#### Scenario: Corrupt config file falls back to defaults
- **WHEN** `data/appsettings.Override.json` is corrupt or unreadable at startup
- **THEN** njord SHALL start with the values from `appsettings*.json` and environment variables
- **AND** njord SHALL log an Error that names the override file and the parse or read problem
- **AND** the corrupt file SHALL remain unchanged on disk

#### Scenario: Valid JSON with invalid values still fails startup
- **WHEN** `data/appsettings.Override.json` is valid JSON but sets a value that fails option validation
- **THEN** startup validation SHALL fail as for any other configuration layer

### Requirement: Override file overrides environment variables
Because the override file is loaded as the last configuration source, its values SHALL take precedence over environment variables for the same configuration keys. This ensures that runtime mutations via the AdminService take effect even when environment variables provide a baseline configuration.

#### Scenario: Mutation overrides env var
- **WHEN** environment variable `Njord__Enrichment__Alerts__Enabled=true` is set
- **AND** a mutation sets `Njord:Enrichment:Alerts:Enabled = false`
- **THEN** the reloaded `AlertOptions.Enabled` SHALL be `false`
- **AND** `GetConfig` SHALL return `alerts.enabled = false`

### Requirement: Config mutations trigger IOptionsMonitor change notification
After persisting, `IConfigurationRoot.Reload()` SHALL be called. `ConfigManager`, the only `IOptionsMonitor<T>.OnChange` subscriber, SHALL pick up the change, forward hot changes to the running actors as typed messages (`AnalysisSettingsChanged`, `PollingChanged`) and push a new effective config to the `StreamConfig` subscribers.

#### Scenario: StreamConfig receives update after mutation
- **WHEN** a config mutation persists and reloads
- **THEN** all `StreamConfig` subscribers SHALL receive a new `NjordConfig`

### Requirement: Concurrent mutations are serialized
Config mutations SHALL be serialized via a `SemaphoreSlim(1)` to prevent concurrent writes to the override file.

#### Scenario: Two concurrent mutations are ordered
- **WHEN** two clients call mutation RPCs simultaneously
- **THEN** the mutations SHALL be applied sequentially and each response SHALL reflect the state after its own mutation

### Requirement: Corrupt override reload keeps the last good configuration
When the override file changes while njord runs and the new content cannot be read or parsed, njord SHALL keep the last successfully loaded override values in effect and SHALL log an Error naming the file and the problem. It SHALL NOT fall back silently to the lower configuration layers. Once the file is valid again, the next reload SHALL apply its values.

#### Scenario: Runtime corruption keeps last good values
- **WHEN** the override file sets `ForecastDays = 7` and is later replaced with truncated JSON while njord runs
- **THEN** the effective `ForecastDays` SHALL remain `7`
- **AND** njord SHALL log an Error naming the override file

#### Scenario: Repaired file is applied again
- **WHEN** a corrupt override file is replaced with valid JSON setting `ForecastDays = 9`
- **THEN** the next reload SHALL apply `ForecastDays = 9`

### Requirement: Mutation over a corrupt override file preserves it
When a config mutation is applied while the override file on disk cannot be parsed, njord SHALL first move the corrupt file aside to `appsettings.Override.json.corrupt-<UTC timestamp>` in the same directory and log an Error naming both paths. It SHALL then write a new override file containing the last successfully loaded override values (none if the file was never loaded successfully) merged with the mutation, so the persisted file matches the running configuration. The corrupt content SHALL never be deleted or overwritten by njord.

#### Scenario: Corrupt file is moved aside before the first write
- **WHEN** njord started with a corrupt override file and a mutation sets `ForecastDays = 7`
- **THEN** the original bytes SHALL be preserved in `appsettings.Override.json.corrupt-<UTC timestamp>`
- **AND** `data/appsettings.Override.json` SHALL contain only the mutation
- **AND** njord SHALL log an Error naming the preserved file

#### Scenario: Last good values survive the rewrite
- **WHEN** the override file set `Horizons = [6, 24]`, then turned corrupt at runtime, and a mutation sets `ForecastDays = 7`
- **THEN** the new override file SHALL contain both `Horizons = [6, 24]` and `ForecastDays = 7`

### Requirement: Config write logic lives in a dedicated handler class

The system SHALL provide a `ConfigUpdateHandler` class that owns `IConfigWriter` and the validation + persistence logic for config updates. `ConfigManager` SHALL delegate `UpdateLocations`, `UpdateSettings`, `UpdateAnalysisConfig`, and `UpdateBudget` messages to this handler. The handler SHALL be a plain sealed class, not an actor.

#### Scenario: Write delegation preserves behavior
- **WHEN** `ConfigManager` receives an `UpdateLocations` message
- **THEN** it SHALL delegate to `ConfigUpdateHandler` which validates and writes, and the response SHALL be identical to the current behavior

#### Scenario: Reload triggers automatically after write
- **WHEN** `ConfigUpdateHandler` writes an override via `IConfigWriter`
- **THEN** `IOptionsMonitor` change callbacks SHALL fire automatically, and `ConfigManager` SHALL re-read and propagate without explicit notification from the handler

