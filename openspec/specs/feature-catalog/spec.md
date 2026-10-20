# feature-catalog Specification

## Purpose
TBD - created by archiving change feature-centric-config. Update Purpose after archive.
## Requirements
### Requirement: Features are identified by a closed kind
Every configurable capability SHALL be identified by a member of a closed feature-kind enumeration, never by a free string, and SHALL be described by a descriptor with that kind, a category (WeatherAlerts, AirQuality, Lifestyle, Marine, Flood, Solar or Seasonal), a human-readable description, a default enabled state, required parameters and optional parameters.

#### Scenario: Descriptor content
- **WHEN** the FrostAlert feature is inspected
- **THEN** it reports kind FrostAlert, category WeatherAlerts, a non-empty description, its default state and its required weather parameters

#### Scenario: Optional parameters do not gate a feature
- **WHEN** a feature declares an optional parameter that the fetched data lacks
- **THEN** the feature SHALL stay active and be evaluated without that parameter

#### Scenario: Kinds are unique
- **WHEN** all registered features are listed
- **THEN** no two share a kind

### Requirement: Feature kinds map to configuration keys explicitly
Each feature kind SHALL map to its configuration key through an explicit mapping covering every member, and the key SHALL equal the member name.

#### Scenario: Every member pinned
- **WHEN** the mapping is evaluated for each feature kind
- **THEN** it returns the member name, and an undefined kind throws an out-of-range error

### Requirement: Handlers expose a feature instead of a config key
Every domain handler SHALL expose a feature descriptor and SHALL NOT expose a separate config key or requirements property.

#### Scenario: Handler registered
- **WHEN** a handler is registered
- **THEN** its feature kind has a key in the `Njord:Features` section

### Requirement: Features are toggled individually
The system SHALL read one boolean per feature from `Njord:Features`, defaulting to the descriptor default, and SHALL reject unknown keys at startup.

#### Scenario: Disable a single alert
- **WHEN** `Njord:Features:StormAlert` is false and all other defaults apply
- **THEN** no storm alerts are produced and all other default alerts still are

#### Scenario: Unknown key
- **WHEN** `Njord:Features:FrostAlertt` is configured
- **THEN** startup validation fails naming the unknown key

### Requirement: Weather alerts are enabled per rule
Each weather alert rule SHALL have its own feature, and the weather alert handler SHALL evaluate only rules whose feature is enabled.

#### Scenario: Rule disabled
- **WHEN** the FrostAlert feature is disabled and temperature drops below 0 C
- **THEN** no frost alert is emitted while other enabled rules still evaluate

#### Scenario: All rules disabled
- **WHEN** every weather alert feature is disabled
- **THEN** the weather alert handler is inactive and contributes no required parameters or domains

### Requirement: Legacy enrichment keys migrate
The system SHALL read legacy `Njord:Enrichments:*` values, expand `WeatherAlerts` to all individual alert features, map every other legacy key to the feature of the same name, give explicit `Njord:Features` values precedence, and log a warning listing the legacy keys found.

#### Scenario: Legacy WeatherAlerts expands
- **WHEN** `Njord:Enrichments:WeatherAlerts` is false and no `Njord:Features` entries exist
- **THEN** all individual weather alert features are disabled

#### Scenario: Explicit feature wins
- **WHEN** `Njord:Enrichments:WeatherAlerts` is false and `Njord:Features:FrostAlert` is true
- **THEN** FrostAlert is enabled and the other alert features are disabled

### Requirement: Feature resolution reports requirements and cost
Feature resolution SHALL expose all registered features, the enabled features, the union of their required parameters, the required domains, and the marginal monthly API cost of each feature, computed with the same formula as startup budget validation.

#### Scenario: Domain already active
- **WHEN** CommuteSafety is enabled and FrostAlert is evaluated
- **THEN** FrostAlert's cost is 0 calls because Weather is already required

#### Scenario: Feature adds a domain
- **WHEN** no enabled feature needs AirQuality and PollenAlert is evaluated for one location at 60 minute polling
- **THEN** its cost equals the monthly AirQuality call count of that location

#### Scenario: Cost matches startup validation
- **WHEN** the total of enabled features is computed
- **THEN** it equals the projected usage `BudgetGuard` validates at startup

