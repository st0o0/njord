## ADDED Requirements

### Requirement: SnowDepthWatch reports snow accumulation status
The system SHALL provide a `SnowDepthWatch` condition that evaluates `snow_depth` over the forecast horizon. The condition SHALL report the maximum predicted snow depth and a severity level: light (>2 cm), moderate (>10 cm), heavy (>25 cm), extreme (>50 cm). When the maximum snow depth is below 2 cm, the condition SHALL not fire.

#### Scenario: Moderate snow cover
- **WHEN** the maximum `snow_depth` in the forecast is 15 cm
- **THEN** the condition SHALL fire with level moderate and report 15 cm

#### Scenario: No snow
- **WHEN** all `snow_depth` values are below 2 cm
- **THEN** the condition SHALL not fire

#### Scenario: Extreme snow depth
- **WHEN** the maximum `snow_depth` exceeds 50 cm
- **THEN** the condition SHALL fire with level extreme

### Requirement: SnowDepthWatch requires only Weather
The feature descriptor SHALL declare `Weather: SnowDepth` in its domain requirements. It SHALL NOT require Ensemble; confidence SHALL be `ConfidenceLevel.Medium`.

#### Scenario: No ensemble needed
- **WHEN** only the Weather domain has been polled
- **THEN** the condition SHALL evaluate with Medium confidence

### Requirement: SnowDepthWatch is disabled by default
The feature descriptor SHALL set `DefaultEnabled = false`. Snow depth is irrelevant for many locations.

#### Scenario: Must be explicitly enabled
- **WHEN** no explicit configuration is provided
- **THEN** the condition SHALL not be active
