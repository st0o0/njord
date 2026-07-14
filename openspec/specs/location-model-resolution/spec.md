# location-model-resolution Specification

## Purpose

Resolution of effective weather models per location by merging global and per-location model lists, with static coverage validation against known model bounding boxes at startup.

## Requirements

### Requirement: LocationOptions resolves effective models by merging
The `LocationOptions` SHALL provide a method to resolve effective models
by merging the global `Models` list with the location-specific `Models`
list, deduplicated. If the location has no `Models` configured, the
effective list SHALL equal the global list.

#### Scenario: Location with additional models merges with global
- **WHEN** global Models is `["icon_global", "icon_eu"]` and location
  Models is `["icon_d2"]`
- **THEN** the resolved list SHALL be `["icon_global", "icon_eu", "icon_d2"]`

#### Scenario: Location without models gets global list
- **WHEN** global Models is `["icon_global", "icon_eu"]` and location
  Models is null
- **THEN** the resolved list SHALL be `["icon_global", "icon_eu"]`

#### Scenario: Duplicate models are deduplicated
- **WHEN** global Models is `["icon_global", "icon_eu"]` and location
  Models is `["icon_eu", "icon_d2"]`
- **THEN** the resolved list SHALL be `["icon_global", "icon_eu", "icon_d2"]`

### Requirement: ModelCoverageRegistry provides static coverage data
The system SHALL provide a `ModelCoverageRegistry` with coverage data for
all documented Open-Meteo models. Each entry SHALL have a coverage tier
(`Global`, `Europe`, or `Regional`) and an optional bounding box for
regional models.

#### Scenario: Global model has no bounding box
- **WHEN** the registry is queried for `"icon_global"`
- **THEN** it SHALL return `Global` tier with no bounding box

#### Scenario: Regional model has a bounding box
- **WHEN** the registry is queried for `"icon_d2"`
- **THEN** it SHALL return `Regional` tier with a bounding box covering
  DE, CH, AT (~43-57N, 1-18E)

#### Scenario: Unknown model returns null
- **WHEN** the registry is queried for `"new_future_model"`
- **THEN** it SHALL return null

### Requirement: Startup validates model coverage for each location
The `NjordOptionsValidator` SHALL resolve effective models per location
and check each (location, model) pair against the `ModelCoverageRegistry`.
If a location's coordinates fall outside a model's bounding box, the
validator SHALL log a warning. Unknown model IDs SHALL produce a separate
warning but SHALL NOT block startup.

#### Scenario: Regional model within coverage passes silently
- **WHEN** location "berlin" (51.84N, 13.41E) has model `"icon_d2"`
  (box: 43-57N, 1-18E)
- **THEN** no warning SHALL be logged for this pair

#### Scenario: Regional model outside coverage produces warning
- **WHEN** location "amsterdam" (52.37N, 4.90E) has model
  `"meteoswiss_icon_ch1"` (box: 44-50N, 4-12E)
- **THEN** a warning SHALL be logged indicating the model may not cover
  this location

#### Scenario: Unknown model produces warning but does not block
- **WHEN** location "berlin" has model `"new_future_model"` not in the
  registry
- **THEN** a warning SHALL be logged about the unknown model but startup
  SHALL proceed

### Requirement: Bounding box check is generous
Bounding boxes SHALL be intentionally generous (1-2 degree padding beyond
documented coverage). A location at the boundary of documented coverage
SHALL pass validation. The runtime HTTP 400 handler in SchedulerActor
serves as the safety net for borderline cases.

#### Scenario: Location at boundary passes
- **WHEN** location coordinates are within 1 degree of a model's documented
  boundary
- **THEN** validation SHALL pass (no warning)
