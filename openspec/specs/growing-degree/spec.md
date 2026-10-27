# growing-degree Specification

## Purpose

GrowingDegree forecast computing Growing Degree Days (GDD) accumulation over the forecast horizon for agricultural planning.

## Requirements

### Requirement: GrowingDegree forecast accumulates degree days
The system SHALL provide a `GrowingDegree` forecast in the Lifestyle category that computes Growing Degree Days (GDD) over the forecast horizon. For each day, GDD equals max(mean_temperature - base_temperature, 0). The forecast reports the daily GDD values and the cumulative total.

#### Scenario: Warm day accumulates GDD
- **WHEN** the mean temperature for a day is 18C and the base temperature is 5C
- **THEN** that day SHALL contribute 13 GDD to the accumulation

#### Scenario: Cold day contributes zero
- **WHEN** the mean temperature for a day is 3C and the base temperature is 5C
- **THEN** that day SHALL contribute 0 GDD (never negative)

#### Scenario: Multi-day accumulation
- **WHEN** the forecast covers 4 days with mean temperatures 10C, 15C, 8C, 20C and base 5C
- **THEN** the cumulative GDD SHALL be 5 + 10 + 3 + 15 = 33

### Requirement: GrowingDegree config
The `GrowingDegreeConfig` SHALL support a configurable base temperature (default 5C). Section `Njord:Features:Lifestyle:GrowingDegree`. Default disabled.

#### Scenario: Custom base temperature
- **WHEN** the operator sets base temperature to 10C (common for corn)
- **THEN** GDD computation SHALL use 10C as the base
