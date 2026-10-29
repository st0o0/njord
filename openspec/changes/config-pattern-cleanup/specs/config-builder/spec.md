## MODIFIED Requirements

### Requirement: Features are grouped by category with per-feature cost
The builder SHALL list all features grouped by feature category. Each feature SHALL show its name, description, required domains and its marginal cost. Feature keys in `features.json` SHALL be the feature name only (e.g. `"Frost"`), with `category` as a separate metadata field used for UI grouping.

#### Scenario: Feature key is flat
- **WHEN** `features.json` is inspected
- **THEN** each entry's `key` SHALL be the feature name without a category prefix (e.g. `"Frost"`, not `"Forecast:Frost"`)

#### Scenario: Category is metadata only
- **WHEN** the builder groups features for display
- **THEN** it SHALL use the `category` field from `features.json`, not the feature key

### Requirement: Export as appsettings.json
The builder SHALL generate a complete `appsettings.json` matching the njord configuration schema. Feature toggles and options SHALL be exported under a flat `Features` object keyed by feature name. Weather settings SHALL be under `Njord.Weather`. Only non-default values SHALL be included.

#### Scenario: Feature toggle in JSON export
- **WHEN** a user disables Frost (default enabled)
- **THEN** the exported JSON SHALL contain `"Features": { "Frost": { "Enabled": false } }`

#### Scenario: Feature option in JSON export
- **WHEN** a user changes Frost thresholds to `[-1, -10, -20]`
- **THEN** the exported JSON SHALL contain `"Features": { "Frost": { "Thresholds": [-1, -10, -20] } }`

#### Scenario: Weather section in JSON export
- **WHEN** a user changes forecast days to 7
- **THEN** the exported JSON SHALL contain `"Weather": { "ForecastDays": 7 }`

### Requirement: Export as environment variables
The builder SHALL generate the equivalent configuration as environment variables using the .NET `__` separator convention. Feature env vars SHALL use the flat path `Njord__Features__<Name>__<Setting>`.

#### Scenario: Feature toggle as env var
- **WHEN** a user disables Frost
- **THEN** the exported env var SHALL be `Njord__Features__Frost__Enabled=false`

#### Scenario: Feature threshold array as env vars
- **WHEN** a user sets Frost thresholds to `[-1, -10, -20]`
- **THEN** the exported env vars SHALL be `Njord__Features__Frost__Thresholds__0=-1`, `...__1=-10`, `...__2=-20`

#### Scenario: Weather env var
- **WHEN** a user changes forecast days
- **THEN** the exported env var SHALL be `Njord__Weather__ForecastDays=7`

### Requirement: Import existing appsettings.json
The builder SHALL accept a pasted `appsettings.json` and populate all builder fields from it. Feature settings SHALL be read from the flat `Features` object. Unknown keys SHALL be ignored.

#### Scenario: Import flat feature config
- **WHEN** a user pastes JSON containing `"Features": { "Frost": { "Enabled": false } }`
- **THEN** the builder SHALL show Frost as disabled

### Requirement: Import existing environment variables
The builder SHALL accept pasted environment variables and populate the builder fields. Feature env vars SHALL be parsed from the flat path `Njord__Features__<Name>__<Setting>`.

#### Scenario: Import flat feature env var
- **WHEN** a user pastes `Njord__Features__Frost__Enabled=false`
- **THEN** the builder SHALL show Frost as disabled

### Requirement: Builder state persists in URL hash
The builder SHALL encode its current state in the URL fragment (hash) so that configurations can be shared via link. Loading a URL with a hash SHALL restore the builder state. Feature keys in the hash SHALL use flat names.

#### Scenario: Shareable link with flat keys
- **WHEN** a user configures features and shares the URL
- **THEN** the hash SHALL contain flat feature keys and the recipient SHALL see the same configuration
