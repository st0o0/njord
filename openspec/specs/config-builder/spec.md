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

### Requirement: Parameter group selector shows API weight impact
The builder SHALL allow selecting parameter groups (Weather, Solar, Soil) and show the resulting hourly variable count and API call weight (`ceil(count/10)`).

#### Scenario: Adding Solar group shows weight change
- **WHEN** a user enables the Solar group alongside Weather
- **THEN** the variable count changes from 30 to 39 and weight from 3 to 4

### Requirement: Live budget calculator validates configuration
The builder SHALL compute and display projected monthly API usage using the same formula as `NjordOptionsValidator`: `totalModelsPerCycle × cyclesPerMonth × apiCallWeight`. It SHALL show the projection against the budget (default 300k or override), display a percentage bar, and warn when exceeding the 80% guard.

#### Scenario: Budget bar updates on model change
- **WHEN** a user adds a new model to all locations
- **THEN** the budget bar updates immediately to reflect the increased projected usage

#### Scenario: Over-budget warning
- **WHEN** projected usage exceeds 80% of the budget
- **THEN** a warning is displayed with suggestions to reduce usage

### Requirement: Export as appsettings.json
The builder SHALL generate a complete `appsettings.json` matching the njord configuration schema. A copy-to-clipboard button SHALL be provided. The JSON SHALL include only non-default values to keep the output concise.

#### Scenario: Copy appsettings.json
- **WHEN** a user clicks "Copy as appsettings.json"
- **THEN** valid JSON is copied to the clipboard that njord can consume directly

### Requirement: Export as environment variables
The builder SHALL generate the equivalent configuration as environment variables using the .NET `__` separator convention (e.g., `Njord__Models__0=icon_d2`). A copy-to-clipboard button SHALL be provided.

#### Scenario: Copy env vars
- **WHEN** a user clicks "Copy as env vars"
- **THEN** a newline-separated list of `KEY=VALUE` pairs is copied to the clipboard

### Requirement: Import existing appsettings.json
The builder SHALL accept a pasted `appsettings.json` and populate all builder fields from it. Unknown keys SHALL be ignored. Invalid JSON SHALL show an error message.

#### Scenario: Import populates builder
- **WHEN** a user pastes a valid appsettings.json with 2 locations and 5 models
- **THEN** the builder shows those 2 locations and 5 models with all other settings populated

### Requirement: Import existing environment variables
The builder SHALL accept pasted environment variables (one `KEY=VALUE` per line, keys starting with `Njord__`) and populate the builder fields. Non-Njord keys SHALL be ignored. The builder SHALL also accept docker-compose `environment:` syntax where lines are prefixed with `- ` and optional whitespace.

#### Scenario: Import env vars
- **WHEN** a user pastes lines containing `Njord__Mqtt__Host=192.168.1.1` and `Njord__Models__0=icon_d2`
- **THEN** the builder shows MQTT host as 192.168.1.1 and icon_d2 in the model list

#### Scenario: Import compose-style env vars
- **WHEN** a user pastes lines like `      - Njord__Horizons__0=3` with leading whitespace and list markers
- **THEN** the builder strips the formatting and imports the horizons correctly

### Requirement: Import docker-compose environment blocks
The builder SHALL accept pasted docker-compose YAML containing an `environment:` block with `- KEY=VALUE` list items. Lines with leading whitespace and `- ` prefix SHALL be stripped before parsing. Non-`Njord__` lines SHALL be ignored. The full compose service block (including `image:`, `volumes:`, etc.) SHALL be accepted — only environment lines are extracted.

#### Scenario: Import compose environment block
- **WHEN** a user pastes a docker-compose snippet containing `- Njord__PollInterval=01:00:00` and `- Njord__Models__0=icon_d2`
- **THEN** the builder populates PollInterval as "01:00:00" and shows icon_d2 in the model list

#### Scenario: Import full compose service block
- **WHEN** a user pastes a complete compose service block including `image:`, `volumes:`, and `environment:` sections
- **THEN** only the environment variables are parsed; non-environment YAML is ignored

#### Scenario: Mixed Njord and non-Njord env vars
- **WHEN** a user pastes a compose environment block containing `- TZ=Europe/Berlin` and `- Njord__ForecastDays=4`
- **THEN** only `Njord__ForecastDays=4` is imported; `TZ` is ignored

### Requirement: Enrichment toggles with per-feature settings
The builder SHALL show toggles for each enrichment feature (consensus, alerts, derived, trends, indices, energy, history). When expanded, each feature SHALL show its configurable settings with defaults pre-filled.

#### Scenario: Enable energy with custom settings
- **WHEN** a user enables the Energy feature and changes FlowTemp to 40.0
- **THEN** the generated config includes `"Energy": { "Enabled": true, "FlowTemp": 40.0, ... }`

### Requirement: Builder state persists in URL hash
The builder SHALL encode its current state in the URL fragment (hash) so that configurations can be shared via link. Loading a URL with a hash SHALL restore the builder state.

#### Scenario: Shareable link
- **WHEN** a user configures 2 locations and 3 models
- **THEN** the URL hash updates and sharing that URL restores the same configuration
