## ADDED Requirements

### Requirement: Effective models per location are the union of global and location models
The effective model list of a location SHALL be the case-insensitive union of the global `NjordOptions.Models` list followed by the location's own `Models` list, preserving first occurrence order. If the location has no `Models`, the effective list SHALL equal the global list. `PollScheduler`, `NjordOptionsValidator`, `BudgetCalculator` and `ConfigSnapshotMapping` SHALL all resolve models this way; `LocationOptions` itself SHALL stay a plain options class without a resolve method.

#### Scenario: Location with additional models merges with global
- **WHEN** global Models is `["icon_global", "icon_eu"]` and location Models is `["icon_d2"]`
- **THEN** the resolved list SHALL be `["icon_global", "icon_eu", "icon_d2"]`

#### Scenario: Location without models gets global list
- **WHEN** global Models is `["icon_global", "icon_eu"]` and location Models is null
- **THEN** the resolved list SHALL be `["icon_global", "icon_eu"]`

#### Scenario: Duplicate models are deduplicated
- **WHEN** global Models is `["icon_global", "icon_eu"]` and location Models is `["ICON_EU", "icon_d2"]`
- **THEN** the resolved list SHALL be `["icon_global", "icon_eu", "icon_d2"]`

### Requirement: Startup rejects models that do not cover a location
`NjordOptionsValidator` SHALL resolve the effective models per location and check each (location, model) pair with `ModelCoverageRegistry.IsPlausible`. A pair whose location lies outside a known model's bounding box SHALL add a validation failure naming the model, the location, its coordinates and the documented coverage, so startup fails. Model ids that are not in the registry SHALL be accepted without a failure (forward compatibility with new Open-Meteo models).

#### Scenario: Regional model within coverage passes silently
- **WHEN** location "berlin" (51.84N, 13.41E) has model `"icon_d2"` (box: 43-57N, 1-18E)
- **THEN** no validation failure is reported for this pair

#### Scenario: Regional model outside coverage fails validation
- **WHEN** location "amsterdam" (52.37N, 4.90E) has model `"meteoswiss_icon_ch1"` (box: 44-50N, 4-12E)
- **THEN** validation fails with a message that the model may not cover this location

#### Scenario: Unknown model is accepted
- **WHEN** location "berlin" has model `"new_future_model"` that is not in the registry
- **THEN** no validation failure is reported for that pair

## MODIFIED Requirements

### Requirement: ModelCoverageRegistry provides static coverage data
The system SHALL provide a `ModelCoverageRegistry` (src/Njord.Weather/ModelCoverageRegistry.cs) with coverage data for all documented Open-Meteo models. Each entry SHALL have a coverage tier (`Global`, `Continental`, or `Regional`), a region description, an optional bounding box for non-global models, and the model's `MaxForecastHours`.

#### Scenario: Global model has no bounding box
- **WHEN** the registry is queried for `"icon_global"`
- **THEN** it SHALL return `Global` tier with no bounding box

#### Scenario: Regional model has a bounding box
- **WHEN** the registry is queried for `"icon_d2"`
- **THEN** it SHALL return `Regional` tier with a bounding box covering DE, CH, AT (43-57N, 1-18E)

#### Scenario: Unknown model returns null
- **WHEN** the registry is queried for `"new_future_model"`
- **THEN** it SHALL return null

### Requirement: Bounding box check is generous
Bounding boxes SHALL be intentionally generous (1-2 degree padding beyond documented coverage). A location at the boundary of documented coverage SHALL pass validation. At runtime, an HTTP 400 "no data for this location" answer SHALL surface as a `ModelUnavailable` fetch failure that `PollScheduler` retries with transient backoff, which serves as the safety net for borderline cases.

#### Scenario: Location at boundary passes
- **WHEN** location coordinates are within 1 degree of a model's documented boundary
- **THEN** validation SHALL pass (no failure)

## REMOVED Requirements

### Requirement: LocationOptions resolves effective models by merging
**Reason**: The requirement named types, files or behavior that no longer exist; it is restated against the current code.
**Migration**: Superseded by "Effective models per location are the union of global and location models" in this capability.

### Requirement: Startup validates model coverage for each location
**Reason**: The requirement named types, files or behavior that no longer exist; it is restated against the current code.
**Migration**: Superseded by "Startup rejects models that do not cover a location" in this capability.
