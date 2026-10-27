## ADDED Requirements

### Requirement: ClearSkyUv alert fires on high clear-sky UV index
The system SHALL provide a `ClearSkyUv` alert that evaluates `uv_index_clear_sky` over the daytime hours of the forecast. The alert SHALL fire when the maximum clear-sky UV index exceeds the configured threshold (default 6). This indicates UV danger even under intermittent clouds -- "if the clouds part, UV reaches this level." Severity SHALL be derived from the peak: moderate (>6), high (>8), extreme (>10).

#### Scenario: High clear-sky UV
- **WHEN** `uv_index_clear_sky` exceeds 8 during daytime hours
- **THEN** the alert SHALL fire with severity high

#### Scenario: Below threshold
- **WHEN** all `uv_index_clear_sky` values are below 6
- **THEN** the alert SHALL not fire

#### Scenario: Only daytime hours evaluated
- **WHEN** `is_day` is 0 for a forecast hour
- **THEN** that hour's `uv_index_clear_sky` SHALL be excluded from evaluation

### Requirement: ClearSkyUv requires only Weather
The feature descriptor SHALL declare `Weather: UvIndexClearSky | IsDay` in its domain requirements. It SHALL NOT require Ensemble; confidence SHALL be `ConfidenceLevel.Medium`.

#### Scenario: No ensemble needed
- **WHEN** only the Weather domain has been polled
- **THEN** the alert SHALL evaluate with Medium confidence

### Requirement: ClearSkyUv is disabled by default
The feature descriptor SHALL set `DefaultEnabled = false`.

#### Scenario: Must be explicitly enabled
- **WHEN** no explicit configuration is provided
- **THEN** the alert SHALL not be active
