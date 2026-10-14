## ADDED Requirements

### Requirement: ConfigWriter provides validated section-scoped read-modify-write
The system SHALL provide `IConfigWriter` (implemented by `ConfigWriter` in `Njord.Core/Configuration/`) with `UpdateAsync<T>(Action<T> applyChanges, CancellationToken)`, which applies the mutation to a clone of the current value of options type `T`, validates the result with `T`'s registered validators, and only if valid persists it under `T`'s section path in `data/appsettings.Override.json` and triggers an `IConfigurationRoot.Reload()`. It SHALL return a `ConfigUpdateResult<T>` carrying whether it was applied, the resulting value, the changed property names and the validation failures; an invalid result SHALL leave the file unchanged. `Check<T>` SHALL validate a mutation without persisting it. There SHALL be no `IWritableOptions<T>` or `WritableNjordOptions`.

#### Scenario: Update mutates and persists
- **WHEN** a caller invokes `UpdateAsync<AlertOptions>(opt => opt.Enabled = false, ct)`
- **THEN** `data/appsettings.Override.json` contains `{"Njord": {"Enrichment": {"Alerts": {"Enabled": false}}}}` (merged with any prior overrides)
- **AND** the reloaded configuration binds `AlertOptions.Enabled` as `false`

#### Scenario: Invalid update is not persisted
- **WHEN** a caller invokes `UpdateAsync<ConsensusOptions>(opt => opt.MinQuorum = 0, ct)`
- **THEN** the result is not applied, carries the validation failure, and `data/appsettings.Override.json` is unchanged

## MODIFIED Requirements

### Requirement: Override file is created on first mutation
The override file SHALL NOT be created on application startup. It SHALL be created only when the first mutation is applied. If the file does not exist when `IConfigWriter.UpdateAsync` is called, it SHALL be created with the mutation content.

#### Scenario: No override file at startup
- **WHEN** njord starts without `data/appsettings.Override.json`
- **THEN** no file SHALL be created in `data/`
- **AND** configuration SHALL come from `appsettings.json` and environment variables

#### Scenario: First mutation creates file
- **WHEN** the first `IConfigWriter.UpdateAsync` call is made
- **THEN** `data/appsettings.Override.json` SHALL be created with the mutated options

### Requirement: Concurrent mutations are serialized
Concurrent calls to `IConfigWriter.UpdateAsync` SHALL be serialized via a `SemaphoreSlim(1)` to prevent lost updates from interleaved read-modify-write cycles.

#### Scenario: Two concurrent updates both apply
- **WHEN** two callers invoke `IConfigWriter.UpdateAsync` simultaneously
- **THEN** both mutations SHALL be applied sequentially and the final file SHALL reflect both changes

## REMOVED Requirements

### Requirement: WritableNjordOptions provides thread-safe read-modify-write
**Reason**: The requirement named types, files or behavior that no longer exist; it is restated against the current code.
**Migration**: Superseded by "ConfigWriter provides validated section-scoped read-modify-write" in this capability.
