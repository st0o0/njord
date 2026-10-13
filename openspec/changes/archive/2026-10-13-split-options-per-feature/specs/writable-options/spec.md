## MODIFIED Requirements

### Requirement: WritableNjordOptions provides thread-safe read-modify-write
The system SHALL provide a section-scoped configuration writer that accepts an update of one options type (`Update<T>(Action<T>)`), applies the mutation to a clone of the current value of `T`, validates the result with `T`'s registered validator, and only if valid persists it under `T`'s section path in `data/appsettings.Override.json` and triggers an `IConfigurationRoot.Reload()`. An invalid result SHALL return the validation failures and SHALL leave the file unchanged.

#### Scenario: Update mutates and persists
- **WHEN** a caller invokes `Update<AlertOptions>(opt => opt.Enabled = false)`
- **THEN** `data/appsettings.Override.json` SHALL contain `{"Njord": {"Enrichment": {"Alerts": {"Enabled": false}}}}` (merged with any prior overrides)
- **AND** `IOptionsMonitor<AlertOptions>.CurrentValue.Enabled` SHALL be `false` after reload

#### Scenario: Invalid update is not persisted
- **WHEN** a caller invokes `Update<ConsensusOptions>(opt => opt.MinQuorum = 0)`
- **THEN** the update returns a validation failure and `data/appsettings.Override.json` is unchanged

### Requirement: Reload triggers IOptionsMonitor change notification
After writing the override file, the writer SHALL call `IConfigurationRoot.Reload()`. The configuration manager SHALL be the only subscriber to the resulting change notifications. It SHALL forward hot changes to the running actors as typed messages and SHALL serve the admin `StreamConfig` subscribers with the updated configuration.

#### Scenario: StreamConfig receives update after mutation
- **WHEN** a mutation is persisted and reloaded
- **THEN** all `StreamConfig` subscribers SHALL receive a new `NjordConfig` snapshot reflecting the mutation

#### Scenario: Hot change reaches the pipeline
- **WHEN** an alert threshold mutation is persisted and reloaded
- **THEN** the forecast pipeline receives the new alert settings as a message, without subscribing to change notifications itself
