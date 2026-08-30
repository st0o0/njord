# severity-extensions Specification

## Purpose

Upgrades existing alert evaluators (Frost, Storm, Fog, PressureDrop) from single-threshold Yellow-only severity to tiered multi-threshold severity (Yellow/Orange/Red), adding graduated response levels to the original alert types.

## Requirements

### Requirement: Frost alert uses tiered severity thresholds

`AlertEvaluator.EvaluateFrost` SHALL accept `double[] thresholds` (default [0, -5, -15] C) instead of a single threshold. Severity SHALL be determined by the minimum temperature median: at or below thresholds[0] = Yellow, at or below thresholds[1] = Orange, at or below thresholds[2] = Red. Confidence SHALL remain the average agreement at horizons where temperature is at or below the Yellow threshold.

#### Scenario: Light frost
- **WHEN** minimum consensus median temperature is -2 C with thresholds [0, -5, -15]
- **THEN** a Frost alert with severity Yellow is produced

#### Scenario: Moderate frost
- **WHEN** minimum consensus median temperature is -8 C with thresholds [0, -5, -15]
- **THEN** a Frost alert with severity Orange is produced

#### Scenario: Severe frost
- **WHEN** minimum consensus median temperature is -18 C with thresholds [0, -5, -15]
- **THEN** a Frost alert with severity Red is produced

#### Scenario: No frost
- **WHEN** minimum consensus median temperature is 3 C with thresholds [0, -5, -15]
- **THEN** severity is None

#### Scenario: Custom thresholds
- **WHEN** thresholds are configured as [2, 0, -10]
- **THEN** a temperature of 1 C produces Yellow, -1 C produces Orange, -12 C produces Red

### Requirement: Storm alert uses tiered gust thresholds

`AlertEvaluator.EvaluateStorm` SHALL accept `double[] thresholds` (default [17, 25, 33] m/s) instead of a single threshold. Severity SHALL be determined by the maximum gust median: at or above thresholds[0] = Yellow, at or above thresholds[1] = Orange, at or above thresholds[2] = Red.

#### Scenario: Storm-force gusts
- **WHEN** maximum consensus median gust is 20 m/s with thresholds [17, 25, 33]
- **THEN** a Storm alert with severity Yellow is produced

#### Scenario: Severe storm gusts
- **WHEN** maximum consensus median gust is 28 m/s with thresholds [17, 25, 33]
- **THEN** a Storm alert with severity Orange is produced

#### Scenario: Hurricane-force gusts
- **WHEN** maximum consensus median gust is 36 m/s with thresholds [17, 25, 33]
- **THEN** a Storm alert with severity Red is produced

#### Scenario: Below storm threshold
- **WHEN** maximum consensus median gust is 12 m/s with thresholds [17, 25, 33]
- **THEN** severity is None

### Requirement: Fog alert escalates to Orange for persistent fog

`AlertEvaluator.EvaluateFog` SHALL produce Orange severity when fog conditions persist for >= a configurable number of hours (default 4). Yellow remains for shorter fog durations (1 to fogPersistentHours - 1).

#### Scenario: Brief fog
- **WHEN** fog conditions are met for 2 hours with persistent threshold 4
- **THEN** a Fog alert with severity Yellow is produced

#### Scenario: Persistent fog
- **WHEN** fog conditions are met for 6 hours with persistent threshold 4
- **THEN** a Fog alert with severity Orange is produced

#### Scenario: Custom persistent threshold
- **WHEN** fogPersistentHours is configured as 3 and fog conditions persist for 3 hours
- **THEN** a Fog alert with severity Orange is produced

### Requirement: PressureDrop alert escalates to Orange for severe drops

`AlertEvaluator.EvaluatePressureDrop` SHALL produce Orange severity when the maximum 3-hour pressure drop exceeds a severe threshold (default 10.0 hPa). Yellow remains for drops between the base threshold (5 hPa) and the severe threshold.

#### Scenario: Moderate pressure drop
- **WHEN** maximum 3-hour pressure drop is 7 hPa with thresholds 5/10
- **THEN** a PressureDrop alert with severity Yellow is produced

#### Scenario: Severe pressure drop
- **WHEN** maximum 3-hour pressure drop is 12 hPa with thresholds 5/10
- **THEN** a PressureDrop alert with severity Orange is produced

#### Scenario: Below threshold
- **WHEN** maximum 3-hour pressure drop is 3 hPa with thresholds 5/10
- **THEN** severity is None
