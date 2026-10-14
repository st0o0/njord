## MODIFIED Requirements

### Requirement: Override file overrides environment variables
Because the override file is loaded as the last configuration source, its values SHALL take precedence over environment variables for the same configuration keys. This ensures that runtime mutations via the AdminService take effect even when environment variables provide a baseline configuration.

#### Scenario: Mutation overrides env var
- **WHEN** environment variable `Njord__Enrichment__Alerts__Enabled=true` is set
- **AND** a mutation sets `Njord:Enrichment:Alerts:Enabled = false`
- **THEN** the reloaded `AlertOptions.Enabled` SHALL be `false`
- **AND** `GetConfig` SHALL return `alerts.enabled = false`

### Requirement: Config mutations trigger IOptionsMonitor change notification
After persisting, `IConfigurationRoot.Reload()` SHALL be called. `ConfigManager`, the only `IOptionsMonitor<T>.OnChange` subscriber, SHALL pick up the change, forward hot changes to the running actors as typed messages (`AnalysisSettingsChanged`, `PollingChanged`) and push a new snapshot to the `StreamConfig` subscribers.

#### Scenario: StreamConfig receives update after mutation
- **WHEN** a config mutation persists and reloads
- **THEN** all `StreamConfig` subscribers SHALL receive a new `NjordConfig` snapshot
