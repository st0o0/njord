## MODIFIED Requirements

### Requirement: Config mutations persist to override file
All config mutations SHALL persist user overrides to `data/appsettings.Override.json` (was `data/njord-config.json`). The file SHALL be written atomically (write to temp file, then rename). The content SHALL be wrapped in a `{"Njord": {...}}` section to match the `NjordOptions.SectionName` configuration binding. On startup, njord SHALL load the override file as the last configuration source, giving it higher priority than environment variables.

#### Scenario: Mutation persists to file
- **WHEN** a config mutation succeeds
- **THEN** `data/appsettings.Override.json` SHALL be updated with the section-wrapped config state

#### Scenario: Config survives restart
- **WHEN** njord restarts after a config mutation
- **THEN** the mutated config SHALL be loaded from `data/appsettings.Override.json` and applied

#### Scenario: Corrupt config file falls back to defaults
- **WHEN** `data/appsettings.Override.json` is corrupt or unreadable
- **THEN** njord SHALL start with `appsettings.json` defaults and log a warning

### Requirement: Override file overrides environment variables
Because the override file is loaded as the last configuration source, its values SHALL take precedence over environment variables for the same configuration keys. This ensures that runtime mutations via the AdminService take effect even when environment variables provide a baseline configuration.

#### Scenario: Mutation overrides env var
- **WHEN** environment variable `Njord__Enrichment__Alerts__Enabled=true` is set
- **AND** a mutation sets `Enrichment.Alerts.Enabled = false`
- **THEN** `IOptionsMonitor<NjordOptions>.CurrentValue.Enrichment.Alerts.Enabled` SHALL be `false`
- **AND** `GetConfig` SHALL return `alerts.enabled = false`

### Requirement: Config mutations trigger IOptionsMonitor change notification
After persisting, `IConfigurationRoot.Reload()` SHALL be called to trigger `IOptionsMonitor<NjordOptions>.OnChange`, propagating the new config to all subscribers including `StreamConfig` and actor-level consumers.

#### Scenario: StreamConfig receives update after mutation
- **WHEN** a config mutation persists and reloads
- **THEN** all `StreamConfig` subscribers SHALL receive a new `NjordConfig` snapshot

### Requirement: Concurrent mutations are serialized
Config mutations SHALL be serialized via a `SemaphoreSlim(1)` to prevent concurrent writes to the override file.

#### Scenario: Two concurrent mutations are ordered
- **WHEN** two clients call mutation RPCs simultaneously
- **THEN** the mutations SHALL be applied sequentially and each response SHALL reflect the state after its own mutation

## REMOVED Requirements

### Requirement: ConfigPersistence class
**Reason**: Replaced by `WritableNjordOptions` which implements the `IWritableOptions<NjordOptions>` pattern. The separate `ConfigPersistence` class is no longer needed.
**Migration**: Replace `ConfigPersistence` injection with `IWritableOptions<NjordOptions>`.
