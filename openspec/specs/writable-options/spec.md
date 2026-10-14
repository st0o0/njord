# writable-options Specification

## Purpose

Runtime configuration mutations through `IConfigWriter`/`ConfigWriter`: validated, section-scoped read-modify-write of `data/appsettings.Override.json` with section-wrapped JSON, lazy file creation, atomic and serialized writes, and `IConfigurationRoot.Reload()` propagation to the configuration manager.
## Requirements
### Requirement: Override file uses section-wrapped JSON
The override file SHALL wrap all persisted options under the `"Njord"` section key, matching the `NjordOptions.SectionName` binding. This ensures the persisted keys align with `IConfiguration` section binding and override environment variables (which bind to the same `Njord:*` key path). The override file configuration source SHALL have higher priority than environment variables so that array properties (e.g. `Horizons`, threshold lists) are fully replaced rather than merged by index.

#### Scenario: Section wrapper matches configuration binding
- **WHEN** `data/appsettings.Override.json` contains `{"Njord": {"Horizons": [6, 24]}}`
- **AND** environment variable `Njord__Horizons__0=3` is set
- **THEN** `IOptionsMonitor<NjordOptions>.CurrentValue.Horizons` SHALL be `[6, 24]` (file overrides env var)

#### Scenario: Array replacement on successive mutations
- **WHEN** environment variables define `Njord__Horizons__0=3, __1=6, __2=12, __3=24, __4=48, __5=72`
- **AND** a mutation sets `Horizons = [6, 24]`
- **AND** a subsequent `GetConfig` is called
- **THEN** the returned `horizons` SHALL be exactly `[6, 24]` with no duplicated entries from the env vars

#### Scenario: Non-array scalar properties still merge correctly
- **WHEN** environment variable `Njord__ForecastDays=4` is set
- **AND** a mutation sets `PollInterval = 1800s` (without touching ForecastDays)
- **THEN** `ForecastDays` SHALL remain `4` and `PollInterval` SHALL be `1800s`

### Requirement: Override file is created on first mutation
The override file SHALL NOT be created on application startup. It SHALL be created only when the first mutation is applied. If the file does not exist when `IConfigWriter.UpdateAsync` is called, it SHALL be created with the mutation content.

#### Scenario: No override file at startup
- **WHEN** njord starts without `data/appsettings.Override.json`
- **THEN** no file SHALL be created in `data/`
- **AND** configuration SHALL come from `appsettings.json` and environment variables

#### Scenario: First mutation creates file
- **WHEN** the first `IConfigWriter.UpdateAsync` call is made
- **THEN** `data/appsettings.Override.json` SHALL be created with the mutated options

### Requirement: Atomic file write
The override file SHALL be written atomically: write to a temporary file in the same directory, flush it to disk, then rename/move it to the target path. This prevents partial reads by the file watcher and ensures njord itself never leaves a half-written override file behind.

#### Scenario: Crash during write does not corrupt config
- **WHEN** the process crashes during a write operation
- **THEN** the previous override file (or absence of one) SHALL remain intact

#### Scenario: Successful write leaves no temporary file
- **WHEN** a mutation is persisted
- **THEN** only `data/appsettings.Override.json` SHALL hold the new content and no temporary file SHALL remain in `data/`

### Requirement: Concurrent mutations are serialized
Concurrent calls to `IConfigWriter.UpdateAsync` SHALL be serialized via a `SemaphoreSlim(1)` to prevent lost updates from interleaved read-modify-write cycles.

#### Scenario: Two concurrent updates both apply
- **WHEN** two callers invoke `IConfigWriter.UpdateAsync` simultaneously
- **THEN** both mutations SHALL be applied sequentially and the final file SHALL reflect both changes

### Requirement: Reload triggers IOptionsMonitor change notification
After writing the override file, the writer SHALL call `IConfigurationRoot.Reload()`. The configuration manager SHALL be the only subscriber to the resulting change notifications. It SHALL forward hot changes to the running actors as typed messages and SHALL serve the admin `StreamConfig` subscribers with the updated configuration.

#### Scenario: StreamConfig receives update after mutation
- **WHEN** a mutation is persisted and reloaded
- **THEN** all `StreamConfig` subscribers SHALL receive a new `NjordConfig` snapshot reflecting the mutation

#### Scenario: Hot change reaches the pipeline
- **WHEN** an alert threshold mutation is persisted and reloaded
- **THEN** the forecast pipeline receives the new alert settings as a message, without subscribing to change notifications itself

### Requirement: ConfigWriter provides validated section-scoped read-modify-write
The system SHALL provide `IConfigWriter` (implemented by `ConfigWriter` in `Njord.Core/Configuration/`) with `UpdateAsync<T>(Action<T> applyChanges, CancellationToken)`, which applies the mutation to a clone of the current value of options type `T`, validates the result with `T`'s registered validators, and only if valid persists it under `T`'s section path in `data/appsettings.Override.json` and triggers an `IConfigurationRoot.Reload()`. It SHALL return a `ConfigUpdateResult<T>` carrying whether it was applied, the resulting value, the changed property names and the validation failures; an invalid result SHALL leave the file unchanged. `Check<T>` SHALL validate a mutation without persisting it. There SHALL be no `IWritableOptions<T>` or `WritableNjordOptions`.

#### Scenario: Update mutates and persists
- **WHEN** a caller invokes `UpdateAsync<AlertOptions>(opt => opt.Enabled = false, ct)`
- **THEN** `data/appsettings.Override.json` contains `{"Njord": {"Enrichment": {"Alerts": {"Enabled": false}}}}` (merged with any prior overrides)
- **AND** the reloaded configuration binds `AlertOptions.Enabled` as `false`

#### Scenario: Invalid update is not persisted
- **WHEN** a caller invokes `UpdateAsync<ConsensusOptions>(opt => opt.MinQuorum = 0, ct)`
- **THEN** the result is not applied, carries the validation failure, and `data/appsettings.Override.json` is unchanged

### Requirement: Writer reads every file the configuration loader accepts
The writer SHALL read the existing override file with the same JSON leniency as the configuration loader (comments and trailing commas are accepted), so a hand-edited file that njord loads can also be updated by a mutation. Comments are not preserved when the file is rewritten.

#### Scenario: Hand-edited file with comments is updated
- **WHEN** the override file contains a `//` comment and a trailing comma and sets `ForecastDays = 7`
- **AND** a mutation sets `PollInterval = 1800s`
- **THEN** the mutation SHALL be applied and the new file SHALL contain both `ForecastDays = 7` and `PollInterval = 1800s`

