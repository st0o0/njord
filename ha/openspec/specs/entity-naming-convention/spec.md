## Purpose

Defines naming conventions for entity unique_id slugs, class names, and display names to ensure consistency across all platforms.

## Requirements

### Requirement: Unique ID slugs use descriptive suffixes
Every enrichment sensor's unique_id slug SHALL follow the pattern `{location}_{feature}_{category}` where `{category}` is a descriptive suffix indicating the sensor domain (e.g., `_alert`, `_index`). Slugs SHALL NOT encode units (e.g., `_pct`) or omit the category suffix.

#### Scenario: Sunshine sensor slug has no unit suffix
- **WHEN** a sunshine sensor is created for location "innsbruck"
- **THEN** the unique_id slug is `innsbruck_sunshine`
- **AND** the slug does NOT contain `_pct`

#### Scenario: All enrichment sensor slugs follow the convention
- **WHEN** any enrichment sensor is created
- **THEN** its slug matches the pattern `{location}_{feature}` or `{location}_{feature}_{category}`
- **AND** `{category}` is never a unit abbreviation

### Requirement: Class names match display names
Entity class names SHALL reflect their display name, not an internal concept. When a class is renamed, all references (instantiation, tests, imports) SHALL be updated. No aliases or backward-compatibility shims SHALL be maintained.

#### Scenario: Model performance sensor class name
- **WHEN** the sensor displaying "Model Performance" is defined
- **THEN** the class is named `NjordModelPerformanceSensor`
- **AND** no class named `NjordHistorySensor` exists

### Requirement: Display names do not repeat device context
Entity display names (`_attr_name`) SHALL NOT include information already provided by the parent device name. Entities on a location device SHALL use only the feature name. Entities on the Server device SHALL include enough context to distinguish instances but SHALL NOT repeat the device name.

#### Scenario: Target sensor on Server device
- **WHEN** a target sensor for model "ICON-D2" is created on the Server device
- **THEN** `_attr_name` is `"ICON-D2 Target"`
- **AND** the HA display shows "Server ICON-D2 Target"

#### Scenario: Location enrichment sensor name
- **WHEN** a VPD sensor is created for location "innsbruck"
- **THEN** `_attr_name` is `"VPD"`
- **AND** the HA display shows "Innsbruck VPD"

### Requirement: Slug normalization uses consistent transform
All unique_id slugs SHALL be constructed by applying `.replace("-", "_").replace(" ", "_").lower()` to the raw slug string. This transform SHALL be applied consistently across all entity classes.

#### Scenario: Location with hyphens
- **WHEN** a sensor is created for location "st-gallen"
- **THEN** the slug contains `st_gallen` (hyphens replaced with underscores)

#### Scenario: Location with spaces
- **WHEN** a sensor is created for location "new york"
- **THEN** the slug contains `new_york` (spaces replaced with underscores)
