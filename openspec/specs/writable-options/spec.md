# writable-options Specification

## Purpose

Thread-safe writable options pattern for runtime config mutations.
`WritableNjordOptions` implements `IWritableOptions<NjordOptions>` with
read-modify-write to `data/appsettings.Override.json`, section-wrapped JSON,
atomic file writes, and `IConfigurationRoot.Reload()` propagation.

## Requirements

### Requirement: WritableNjordOptions provides thread-safe read-modify-write
The system SHALL provide an `IWritableOptions<NjordOptions>` implementation (`WritableNjordOptions`) that accepts an `Update(Action<NjordOptions>)` call, applies the mutation to a clone of the current options, persists the result to `data/appsettings.Override.json`, and triggers an `IConfigurationRoot.Reload()` so that `IOptionsMonitor<NjordOptions>` propagates the change to all subscribers.

#### Scenario: Update mutates and persists
- **WHEN** a caller invokes `Update(opt => opt.Enrichment.Alerts.Enabled = false)`
- **THEN** `data/appsettings.Override.json` SHALL contain `{"Njord": {"Enrichment": {"Alerts": {"Enabled": false}}}}` (merged with any prior overrides)
- **AND** `IOptionsMonitor<NjordOptions>.CurrentValue.Enrichment.Alerts.Enabled` SHALL be `false` after reload

### Requirement: Override file uses section-wrapped JSON
The override file SHALL wrap all persisted options under the `"Njord"` section key, matching the `NjordOptions.SectionName` binding. This ensures the persisted keys align with `IConfiguration` section binding and override environment variables (which bind to the same `Njord:*` key path).

#### Scenario: Section wrapper matches configuration binding
- **WHEN** `data/appsettings.Override.json` contains `{"Njord": {"Horizons": [6, 24]}}`
- **AND** environment variable `Njord__Horizons__0=3` is set
- **THEN** `IOptionsMonitor<NjordOptions>.CurrentValue.Horizons` SHALL be `[6, 24]` (file overrides env var)

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
After writing the override file, `WritableNjordOptions` SHALL call `IConfigurationRoot.Reload()` to trigger `IOptionsMonitor<NjordOptions>.OnChange`. This ensures `StreamConfig` subscribers and all actor-level consumers receive the updated configuration.

#### Scenario: StreamConfig receives update after mutation
- **WHEN** a mutation is persisted and reloaded
- **THEN** all `StreamConfig` subscribers SHALL receive a new `NjordConfig` snapshot reflecting the mutation
