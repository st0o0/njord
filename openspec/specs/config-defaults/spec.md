# config-defaults Specification

## Purpose
TBD - created by archiving change config-defaults-in-appsettings. Update Purpose after archive.
## Requirements
### Requirement: Scalar and flag defaults live in the shipped appsettings.json
The shipped `appsettings.json` SHALL contain a `Njord` section with the default value of every scalar, flag, enum and duration option. Option classes SHALL NOT repeat those values as property initializers. The configuration layers keep their order (appsettings, environment, override file), so any layer above can override a default.

#### Scenario: Default poll interval comes from the file
- **WHEN** the host starts with no override and no environment variables
- **THEN** `PollingOptions.PollInterval` is 60 minutes, read from `appsettings.json`

#### Scenario: Environment variable overrides a default
- **WHEN** `Njord__Polling__PollInterval` style environment values or the override file set a value
- **THEN** the configured value wins over the file default

### Requirement: List-valued defaults stay in code
Options whose default is a list or array (`Horizons`, `Parameters.Groups`, threshold arrays) SHALL keep their default in the option class, and a configured list SHALL replace the default completely (`ReplaceCollectionDefaults`), never merge by index.

#### Scenario: Shorter configured list replaces the default
- **WHEN** an override sets `Horizons` to `[6, 24]`
- **THEN** the bound horizons are exactly `[6, 24]`

### Requirement: Reset to defaults means falling back to the shipped file
Removing an override key SHALL make the option read its value from `appsettings.json` (or the code default for lists). The configuration documentation SHALL state this and SHALL reference `appsettings.json` as the single source of scalar defaults.

#### Scenario: Reset a scalar
- **WHEN** a config update resets an enrichment section
- **THEN** the override keys of that section are removed and the effective values equal the `appsettings.json` values

### Requirement: The shipped defaults are verified
A spec SHALL bind the shipped `appsettings.json` into every options type, run each validator against the result without failures, and compare the bound default options with a Verify golden master. Tests that need default options SHALL obtain them through a shared helper that binds the shipped file instead of calling `new XOptions()` and relying on initializers.

#### Scenario: Shipped file is valid
- **WHEN** the shipped `appsettings.json` is bound and validated
- **THEN** all validators succeed

#### Scenario: A default changes
- **WHEN** a value in `appsettings.json` changes
- **THEN** the golden master spec fails until the snapshot is reviewed and accepted

### Requirement: The example file contains no duplicated defaults
`appsettings.Example.json` SHALL contain only deployment-specific keys (locations, models, MQTT broker, optional overrides) with comments. It SHALL NOT restate default values that exist in `appsettings.json`.

#### Scenario: Example stays minimal
- **WHEN** a key of the example file also exists in `appsettings.json` with the same value
- **THEN** a spec flags it as duplicated

