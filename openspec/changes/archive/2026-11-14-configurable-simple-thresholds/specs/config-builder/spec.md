## MODIFIED Requirements

### Requirement: Live budget calculator validates configuration
The builder SHALL compute and display projected monthly API usage using the same formula as `BudgetGuard` (applied at startup by `PollingOptionsValidator`): `totalModelsPerCycle x cyclesPerMonth x apiCallWeight`. It SHALL show the projection against the budget (default 300k or override), display a percentage bar, and warn when exceeding the 80% guard. Feature options that do not affect API calls (thresholds, window sizes, detection limits) SHALL NOT alter the budget calculation.

#### Scenario: Budget bar updates on model change
- **WHEN** a user adds a new model to all locations
- **THEN** the budget bar updates immediately to reflect the increased projected usage

#### Scenario: Over-budget warning
- **WHEN** projected usage exceeds 80% of the budget
- **THEN** a warning is displayed with suggestions to reduce usage

#### Scenario: Threshold options do not affect budget
- **WHEN** a user changes a feature's threshold (e.g., CommuteSafety IceTemperatureCelsius)
- **THEN** the budget calculation SHALL remain unchanged

### Requirement: Feature options displayed in builder
The Config Builder SHALL display per-feature options from `features.json` for all features that have configurable properties beyond `Enabled`. Options SHALL be grouped by category when a feature has more than 3 options. Each option SHALL show its default value, and the builder SHALL include the option in the generated config only when the user changes it from the default.

#### Scenario: Simple threshold option displayed
- **WHEN** a user expands the CommuteSafety feature in the builder
- **THEN** the builder SHALL show input fields for IceTemperatureCelsius, DewPointSpreadCelsius, FogHumidityPercent, HeavyRainMmH, and StrongWindKmh with their defaults

#### Scenario: Only changed options in export
- **WHEN** a user changes only IceTemperatureCelsius to 3.0 and leaves other options at defaults
- **THEN** the exported config SHALL include only `"IceTemperatureCelsius": 3.0` for that feature (plus Enabled if toggled)

#### Scenario: WindowHours validated in builder
- **WHEN** a user sets WindowHours to 0 for ApproachingFront
- **THEN** the builder SHALL show a validation error before export
