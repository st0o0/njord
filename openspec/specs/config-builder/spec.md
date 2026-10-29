# config-builder Specification

## Purpose

An interactive Vue-based configuration builder embedded in the VitePress documentation site that lets users visually assemble their njord configuration, see live budget impact, and export the result as appsettings.json or environment variables. Supports importing existing configs.
## Requirements
### Requirement: Location picker allows adding and removing locations
The builder SHALL provide a form to add locations with name, latitude, and longitude. Users SHALL be able to add per-location models (merged with global). Locations SHALL be removable. At least one location is required.

#### Scenario: Add a location
- **WHEN** a user enters name "borken", latitude 51.84, longitude 6.86 and clicks add
- **THEN** the location appears in the list and is included in the generated config

#### Scenario: Per-location models
- **WHEN** a user adds model "icon_d2" to location "borken"
- **THEN** the generated config includes `"Models": ["icon_d2"]` on that location entry

### Requirement: Model selector shows coverage and horizon info
The builder SHALL display all known models from the exported registry data. Each model SHALL show its coverage tier, region, max forecast hours, and whether it covers each configured location. Models outside a location's coverage SHALL show a warning.

#### Scenario: Coverage warning
- **WHEN** a user selects "meteoswiss_icon_ch1" and has location "borken" (51.84°N, 6.86°E)
- **THEN** a warning indicates that MeteoSwiss CH1 may not cover Borken

#### Scenario: Max horizon displayed
- **WHEN** a user views "icon_d2" in the model selector
- **THEN** it shows "max 48h" and indicates that with ForecastDays=16, only 2 days will be requested

### Requirement: Horizon configuration offers presets and custom input
The builder SHALL offer horizon presets: Standard `[3, 6, 12, 24, 48, 72]`, Fine `[1, 2, 3, 6, 8, 12, 18, 24, 36, 48, 72, 96]`, and a Custom text input. Each horizon value SHALL be between 1 and 96.

#### Scenario: Fine preset selected
- **WHEN** a user selects the "Fine" preset
- **THEN** horizons are set to `[1, 2, 3, 6, 8, 12, 18, 24, 36, 48, 72, 96]`

#### Scenario: Invalid custom horizon rejected
- **WHEN** a user enters "0" or "100" in custom horizons
- **THEN** a validation error is shown

### Requirement: Live budget calculator validates configuration
The builder SHALL compute and display projected monthly API usage using the same formula as `BudgetGuard` (applied at startup by `PollingOptionsValidator`): `totalModelsPerCycle × cyclesPerMonth × apiCallWeight`. It SHALL show the projection against the budget (default 300k or override), display a percentage bar, and warn when exceeding the 80% guard.

#### Scenario: Budget bar updates on model change
- **WHEN** a user adds a new model to all locations
- **THEN** the budget bar updates immediately to reflect the increased projected usage

#### Scenario: Over-budget warning
- **WHEN** projected usage exceeds 80% of the budget
- **THEN** a warning is displayed with suggestions to reduce usage

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

### Requirement: Import docker-compose environment blocks
The builder SHALL accept pasted docker-compose YAML containing an `environment:` block with `- KEY=VALUE` list items. Lines with leading whitespace and `- ` prefix SHALL be stripped before parsing. Non-`Njord__` lines SHALL be ignored. The full compose service block (including `image:`, `volumes:`, etc.) SHALL be accepted — only environment lines are extracted. The builder SHALL read and write the per-concern keys (`Njord__Polling__PollInterval`, `Njord__OpenMeteo__BaseUrl`, `Njord__Storage__PersistencePath`); the removed root-level keys SHALL be reported as outdated on import.

#### Scenario: Import compose environment block
- **WHEN** a user pastes a docker-compose snippet containing `- Njord__Polling__PollInterval=01:00:00` and `- Njord__Models__0=icon_d2`
- **THEN** the builder populates PollInterval as "01:00:00" and shows icon_d2 in the model list

#### Scenario: Import full compose service block
- **WHEN** a user pastes a complete compose service block including `image:`, `volumes:`, and `environment:` sections
- **THEN** only the environment variables are parsed; non-environment YAML is ignored

#### Scenario: Mixed Njord and non-Njord env vars
- **WHEN** a user pastes a compose environment block containing `- TZ=Europe/Berlin` and `- Njord__ForecastDays=4`
- **THEN** only `Njord__ForecastDays=4` is imported; `TZ` is ignored

#### Scenario: Outdated root-level key
- **WHEN** a user pastes `- Njord__PollInterval=01:00:00`
- **THEN** the builder reports the key as outdated and names `Njord__Polling__PollInterval`

### Requirement: Builder state persists in URL hash
The builder SHALL encode its current state in the URL fragment (hash) so that configurations can be shared via link. Loading a URL with a hash SHALL restore the builder state. Feature keys in the hash SHALL use flat names.

#### Scenario: Shareable link with flat keys
- **WHEN** a user configures features and shares the URL
- **THEN** the hash SHALL contain flat feature keys and the recipient SHALL see the same configuration

### Requirement: Features are grouped by category with per-feature cost
The builder SHALL list all features grouped by feature category. Each feature SHALL show its name, description, required domains and its marginal cost. Feature keys in `features.json` SHALL be the feature name only (e.g. `"Frost"`), with `category` as a separate metadata field used for UI grouping.

#### Scenario: Feature key is flat
- **WHEN** `features.json` is inspected
- **THEN** each entry's `key` SHALL be the feature name without a category prefix (e.g. `"Frost"`, not `"Forecast:Frost"`)

#### Scenario: Category is metadata only
- **WHEN** the builder groups features for display
- **THEN** it SHALL use the `category` field from `features.json`, not the feature key

#### Scenario: Cost of a feature that enables a new domain
- **WHEN** no enabled feature needs AirQuality and a user views PollenAlert for one location
- **THEN** it shows the full monthly AirQuality cost

#### Scenario: Cost when domain already active
- **WHEN** Weather is already required and a user views FrostAlert
- **THEN** it shows "+0"

### Requirement: Budget meter reflects the free tier
The builder SHALL show a live meter of projected monthly usage against 300,000 calls, colored green below 50 %, yellow from 50 % to 80 % and red above 80 %.

#### Scenario: Enabling a costly feature
- **WHEN** a user enables a feature that pushes usage past 80 %
- **THEN** the meter turns red and shows used and total calls

#### Scenario: Disabling the last user of a domain
- **WHEN** a user disables the last enabled feature requiring Flood
- **THEN** the usage drops by the Flood cost

### Requirement: Builder exports and imports the feature section
The builder SHALL export `Njord:Features` (JSON and environment variables) and SHALL import both `Njord:Features` and legacy `Njord:Enrichments` input, expanding `WeatherAlerts` to the individual alert features.

#### Scenario: Import legacy config
- **WHEN** a user imports a config with `Enrichments.WeatherAlerts: true`
- **THEN** all individual alert features are enabled and the export uses `Features`

### Requirement: Model selection in the builder
The Config Builder SHALL list the supported weather models with provider, resolution and forecast length, let the user enable them and choose the primary, and emit `Njord:Weather:Models` and the `ModelComparison` feature accordingly.

#### Scenario: Add a model
- **WHEN** the user enables a second model
- **THEN** the exported configuration lists both ids in order and enables `ModelComparison`

#### Scenario: Per-model budget impact
- **WHEN** a model is toggled
- **THEN** the builder shows the added calls per month from the same formula as `BudgetGuard` and updates the budget meter
