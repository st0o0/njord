## MODIFIED Requirements

### Requirement: Features are grouped by category with per-feature cost
The builder SHALL list all features grouped by feature category. Each feature SHALL show its name, description, required domains and its marginal cost. Feature keys in `features.json` SHALL be the feature name only (e.g. `"Frost"`), with `category` as a separate metadata field used for UI grouping. Features with many options SHALL group them into labeled sections (e.g. "Weights", "Scoring Parameters", "Thresholds").

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

#### Scenario: Complex feature options grouped into sections
- **WHEN** the builder displays BbqConfig options
- **THEN** options SHALL be grouped into sections such as "Weights" and "Scoring Parameters"

## ADDED Requirements

### Requirement: Weight sum validation in the builder
The builder SHALL validate that component weight properties for scoring features sum to 1.0. When the sum deviates, the builder SHALL show an inline error with the current sum.

#### Scenario: Valid weights accepted
- **WHEN** a user sets Bbq weights to 0.4, 0.2, 0.25, 0.15
- **THEN** the builder SHALL show the weights as valid (sum 1.0)

#### Scenario: Invalid weights flagged
- **WHEN** a user sets Bbq weights to 0.5, 0.2, 0.3, 0.15
- **THEN** the builder SHALL show an error "Weights sum to 1.15, must be 1.0"

### Requirement: features.json includes options for scoring features
Each scoring feature entry in `features.json` SHALL include an `options` array listing all configurable properties with name, type and default value. Options with a `group` field SHALL be grouped in the builder UI.

#### Scenario: Bbq options in features.json
- **WHEN** the Bbq entry in `features.json` is inspected
- **THEN** it SHALL include options for GoodHourScore, RainThreshold, the four weights, and the band/ramp parameters

#### Scenario: Options with group field
- **WHEN** a feature option has `"group": "Weights"`
- **THEN** the builder SHALL display it under a "Weights" section header
