## ADDED Requirements

### Requirement: Enrichment feature toggles
The Config Builder SHALL display a section titled "Enrichment" containing one toggle per enrichment feature defined in `docs/data/enrichment.json`. Each toggle SHALL show the feature name and indicate whether it is enabled by default. Toggling a feature SHALL update `config.enrichment[featureName].Enabled` in the reactive config state.

#### Scenario: Default state on fresh config
- **WHEN** the user opens the Config Builder with no imported config
- **THEN** enrichment features with `enabledByDefault: true` (Consensus, Alerts, Derived) SHALL be shown as enabled, and features with `enabledByDefault: false` (Trends, Indices, Energy, History) SHALL be shown as disabled

#### Scenario: Toggling a feature on
- **WHEN** the user enables the "Trends" feature toggle
- **THEN** `config.enrichment.Trends.Enabled` SHALL be set to `true`
- **AND** the feature's configurable options SHALL become visible

#### Scenario: Toggling a feature off
- **WHEN** the user disables the "Consensus" feature toggle
- **THEN** `config.enrichment.Consensus` SHALL be removed from the config (omitted features use server defaults)

### Requirement: Enrichment feature options
For each enabled enrichment feature that has configurable options (as defined in `enrichment.json`), the Config Builder SHALL display input fields for each option with the option name, type-appropriate input control, and default value as placeholder.

#### Scenario: Consensus options visible when enabled
- **WHEN** the Consensus feature is enabled
- **THEN** the builder SHALL show inputs for "Method" (text, default "Median") and "TrimPercent" (number, default 0.1)

#### Scenario: Option value included in export
- **WHEN** the user changes the Consensus Method to "TrimmedMean"
- **AND** exports as JSON
- **THEN** the output SHALL include `"Enrichment": { "Consensus": { "Enabled": true, "Method": "TrimmedMean" } }`

#### Scenario: Default options omitted from export
- **WHEN** the user enables a feature but does not change any option values from their defaults
- **THEN** the export SHALL include only `"Enabled": true` for that feature, omitting default-valued options

### Requirement: Persistence provider selection
The Config Builder SHALL display a "Persistence" section with a choice between SQLite (default) and PostgreSQL. When PostgreSQL is selected, a connection string input SHALL appear.

#### Scenario: SQLite selected (default)
- **WHEN** the user has not changed the persistence provider
- **THEN** no persistence section SHALL appear in the exported config (SQLite is the default)

#### Scenario: PostgreSQL selected
- **WHEN** the user selects PostgreSQL and enters a connection string
- **THEN** the export SHALL include `"Persistence": { "Provider": "PostgreSql", "ConnectionString": "..." }`

### Requirement: MQTT authentication fields
The Config Builder SHALL display Username and Password input fields in the MQTT section. The password field SHALL use `type="password"`.

#### Scenario: MQTT credentials in JSON export
- **WHEN** the user enters MQTT username "mqttuser" and password "secret"
- **THEN** the JSON export SHALL include `"Username": "mqttuser"` in the Mqtt section
- **AND** the password SHALL NOT be included in the JSON export (security — passwords should be set via env vars)

#### Scenario: MQTT credentials in env export
- **WHEN** the user enters MQTT username and password
- **THEN** the env export SHALL include `Njord__Mqtt__Username=mqttuser` and `Njord__Mqtt__Password=secret`

### Requirement: Discovery Interval in General section
The Config Builder SHALL expose the Discovery Interval setting in the General section with a default value of "00:20:00".

#### Scenario: Custom discovery interval
- **WHEN** the user changes the discovery interval to "00:30:00"
- **THEN** the export SHALL include `"DiscoveryInterval": "00:30:00"` in the Njord section
