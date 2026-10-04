## Purpose

Provides post-setup configuration via HA's OptionsFlow, allowing users to tune status polling interval and toggle enrichment entity groups.

## Requirements

### Requirement: OptionsFlow is available after setup
The integration SHALL provide an `OptionsFlow` accessible from the integration's configuration page in HA. It SHALL present settings across two steps: general settings (init) and sensor push configuration (sensors).

#### Scenario: User opens options
- **WHEN** the user clicks "Configure" on the njord integration entry
- **THEN** an options form is shown with status poll interval and enrichment group toggles
- **AND** after submitting, a second step for sensor push configuration is shown

### Requirement: Status poll interval option
The OptionsFlow SHALL include a `status_poll_interval` integer field with default 30, minimum 10, and maximum 300 (seconds). The value SHALL be stored in `ConfigEntry.options`.

#### Scenario: User sets poll interval to 60
- **WHEN** the user sets status_poll_interval to 60 and submits
- **THEN** `entry.options["status_poll_interval"]` is 60
- **AND** the status coordinator adopts a 60-second update interval

#### Scenario: Default poll interval
- **WHEN** the integration is set up and no options have been changed
- **THEN** the status coordinator uses 30 seconds

### Requirement: Enrichment group toggles
The OptionsFlow SHALL include a multi-select field for enrichment groups. Available groups: `alerts`, `indices`, `trends`, `derived`, `history`, `consensus`. All groups SHALL be enabled by default. Disabled groups are stored as a list in `entry.options["disabled_enrichment_groups"]`.

#### Scenario: User re-enables a group
- **WHEN** the user re-selects a previously disabled group and submits
- **THEN** the config entry reloads
- **AND** entities for that group are created

### Requirement: Enrichment toggle triggers reload
Changing the enrichment group selection or the sensor push configuration SHALL trigger a config entry reload. Changing only the poll interval SHALL NOT trigger a reload.

#### Scenario: Only poll interval changed
- **WHEN** the user changes only the poll interval
- **THEN** the status coordinator's `update_interval` is updated in-place without reload

#### Scenario: Enrichment groups changed
- **WHEN** the user changes the enrichment group selection
- **THEN** `async_reload` is called on the config entry

#### Scenario: Sensor push mapping changed
- **WHEN** the user changes the sensor push entity mapping
- **THEN** `async_reload` is called on the config entry

### Requirement: Options flow UI strings
`strings.json` and `translations/de.json` SHALL include option step labels, field descriptions, and enrichment group names.

#### Scenario: English strings present
- **WHEN** the options flow is rendered in English
- **THEN** all field labels and descriptions are present from `strings.json`

#### Scenario: German strings present
- **WHEN** the options flow is rendered in German
- **THEN** all field labels and descriptions are present from `translations/de.json`
