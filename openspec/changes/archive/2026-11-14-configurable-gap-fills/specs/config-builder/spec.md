## MODIFIED Requirements

### Requirement: Feature options displayed in builder
The Config Builder SHALL display all configurable options for each feature, grouped by feature. When a feature has more than 5 options, the builder SHALL group them into logical sections (e.g. "Severity Thresholds", "Formula Coefficients", "Classification"). The `features.json` data file SHALL include option metadata (name, type, default, description, min/max where applicable) for every config property.

#### Scenario: New options appear in builder
- **WHEN** a feature gains new config properties (e.g. FireRisk gains ColdCutoff and HumidCutoff)
- **THEN** those properties appear in the builder under the feature's section with their default values

#### Scenario: Options with validation constraints show limits
- **WHEN** a config property has min/max constraints (e.g. FoehnDirectionMin must be 0..360)
- **THEN** the builder input shows the valid range and prevents out-of-range values

#### Scenario: Severity tier options show ordering constraint
- **WHEN** a feature has ordered severity thresholds (e.g. PollenBlast Red > Orange)
- **THEN** the builder validates the ordering and shows a warning if violated
