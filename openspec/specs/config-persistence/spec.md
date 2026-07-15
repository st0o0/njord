# config-persistence Specification

## Purpose

Atomic file-based persistence for runtime config mutations. User overrides are
stored in `data/njord-config.json`, overlaid on `appsettings.json` at startup,
and propagated to all subscribers via `IOptionsMonitor` reload-on-change.

## Requirements

### Requirement: Config mutations persist to njord-config.json
All config mutations SHALL persist user overrides to `data/njord-config.json`. The file SHALL be written atomically (write to temp file, then rename). On startup, njord SHALL load `appsettings.json` first, then overlay `njord-config.json`.

#### Scenario: Mutation persists to file
- **WHEN** a config mutation succeeds
- **THEN** `data/njord-config.json` SHALL be updated with the new config state

#### Scenario: Config survives restart
- **WHEN** njord restarts after a config mutation
- **THEN** the mutated config SHALL be loaded from `njord-config.json` and applied

#### Scenario: Corrupt config file falls back to defaults
- **WHEN** `njord-config.json` is corrupt or unreadable
- **THEN** njord SHALL start with `appsettings.json` defaults and log a warning

### Requirement: Config mutations trigger IOptionsMonitor change notification
After persisting, the file watcher on `njord-config.json` (via `reloadOnChange: true`) SHALL trigger `IOptionsMonitor<NjordOptions>.OnChange`, propagating the new config to all subscribers including `StreamConfig` and actor-level consumers.

#### Scenario: StreamConfig receives update after mutation
- **WHEN** a config mutation persists and reloads
- **THEN** all `StreamConfig` subscribers SHALL receive a new `NjordConfig` snapshot

### Requirement: Concurrent mutations are serialized
Config mutations SHALL be serialized via a lock (e.g., `SemaphoreSlim(1)`) to prevent concurrent writes to `njord-config.json`.

#### Scenario: Two concurrent mutations are ordered
- **WHEN** two clients call mutation RPCs simultaneously
- **THEN** the mutations SHALL be applied sequentially and each response SHALL reflect the state after its own mutation
