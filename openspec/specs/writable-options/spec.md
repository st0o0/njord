# writable-options Specification

## Purpose

Thread-safe writable options pattern for runtime config mutations.
`WritableNjordOptions` implements `IWritableOptions<NjordOptions>` with
read-modify-write to `data/appsettings.Override.json`, section-wrapped JSON,
atomic file writes, and `IConfigurationRoot.Reload()` propagation.
## Requirements
### Requirement: WritableNjordOptions provides thread-safe read-modify-write
The system SHALL provide a section-scoped configuration writer that accepts an update of one options type (`Update<T>(Action<T>)`), applies the mutation to a clone of the current value of `T`, validates the result with `T`'s registered validator, and only if valid persists it under `T`'s section path in `data/appsettings.Override.json` and triggers an `IConfigurationRoot.Reload()`. An invalid result SHALL return the validation failures and SHALL leave the file unchanged.

#### Scenario: Update mutates and persists
- **WHEN** a caller invokes `Update<AlertOptions>(opt => opt.Enabled = false)`
- **THEN** `data/appsettings.Override.json` SHALL contain `{"Njord": {"Enrichment": {"Alerts": {"Enabled": false}}}}` (merged with any prior overrides)
- **AND** `IOptionsMonitor<AlertOptions>.CurrentValue.Enabled` SHALL be `false` after reload

#### Scenario: Invalid update is not persisted
- **WHEN** a caller invokes `Update<ConsensusOptions>(opt => opt.MinQuorum = 0)`
- **THEN** the update returns a validation failure and `data/appsettings.Override.json` is unchanged

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
The override file SHALL NOT be created on application startup. It SHALL be created only when the first mutation is applied. If the file does not exist when `Update` is called, it SHALL be created with the mutation content.

#### Scenario: No override file at startup
- **WHEN** njord starts without `data/appsettings.Override.json`
- **THEN** no file SHALL be created in `data/`
- **AND** configuration SHALL come from `appsettings.json` and environment variables

#### Scenario: First mutation creates file
- **WHEN** the first `Update` call is made
- **THEN** `data/appsettings.Override.json` SHALL be created with the mutated options

### Requirement: Atomic file write
The override file SHALL be written atomically: write to a temporary file in the same directory, then rename/move to the target path. This prevents partial reads by the file watcher.

#### Scenario: Crash during write does not corrupt config
- **WHEN** the process crashes during a write operation
- **THEN** the previous override file (or absence of one) SHALL remain intact

### Requirement: Concurrent mutations are serialized
Concurrent calls to `Update` SHALL be serialized via a `SemaphoreSlim(1)` to prevent lost updates from interleaved read-modify-write cycles.

#### Scenario: Two concurrent updates both apply
- **WHEN** two callers invoke `Update` simultaneously
- **THEN** both mutations SHALL be applied sequentially and the final file SHALL reflect both changes

### Requirement: Reload triggers IOptionsMonitor change notification
After writing the override file, the writer SHALL call `IConfigurationRoot.Reload()`. The configuration manager SHALL be the only subscriber to the resulting change notifications. It SHALL forward hot changes to the running actors as typed messages and SHALL serve the admin `StreamConfig` subscribers with the updated configuration.

#### Scenario: StreamConfig receives update after mutation
- **WHEN** a mutation is persisted and reloaded
- **THEN** all `StreamConfig` subscribers SHALL receive a new `NjordConfig` snapshot reflecting the mutation

#### Scenario: Hot change reaches the pipeline
- **WHEN** an alert threshold mutation is persisted and reloaded
- **THEN** the forecast pipeline receives the new alert settings as a message, without subscribing to change notifications itself

