# enrichment-model-envelope Specification

## Purpose

Per-model computation of Index scores and Energy values, then aggregation into min/max/confidence envelope fields alongside existing single-value outputs. Exposes forecast uncertainty to HA automations.

## Requirements

### Requirement: Per-model index scoring produces an envelope of min/max/confidence
`IndexResult.Compute` SHALL compute the full index score set independently for each model in the snapshot (using that model's hourly data for the location). It SHALL then aggregate across per-model results to produce, for each numeric score field: the existing mean-based value (unchanged), plus `_min` (minimum across models), `_max` (maximum across models), and `_confidence` (fraction of models whose score is within a configurable tolerance of the median score).

#### Scenario: Three models produce different outdoor scores
- **WHEN** model A yields outdoor=80, model B yields outdoor=65, model C yields outdoor=72
- **THEN** IndexResult has outdoor=72 (median-based), outdoor_min=65, outdoor_max=80, outdoor_confidence computed against tolerance

#### Scenario: All models agree closely
- **WHEN** 5 models produce outdoor scores [70, 72, 71, 73, 70] with tolerance=10%
- **THEN** outdoor_confidence is 1.0 (all within ±7.2 of median 71)

#### Scenario: Single model available
- **WHEN** only 1 model provides data for the location
- **THEN** min=max=the single score, confidence=1.0 (trivial agreement)

### Requirement: Per-model energy scoring produces pessimistic/optimistic envelope
`EnergyResult.Compute` SHALL compute energy values independently for each model in the snapshot. It SHALL then expose: existing mean-based values (unchanged), plus `HeatingDemandMax` (worst-case across models), `CopEstimateMin` (lowest COP across models), and `CopOptimalConservative` (hours where ALL models report COP above the threshold).

#### Scenario: Pessimistic heating demand
- **WHEN** 4 models produce heating_demand values [40, 55, 45, 62]
- **THEN** HeatingDemandMax=62 (worst case for heating controller)

#### Scenario: Conservative COP optimal hours
- **WHEN** model A reports COP optimal at hours [2, 3, 4, 5], model B at [3, 4, 5, 6], model C at [4, 5]
- **THEN** CopOptimalConservative=[4, 5] (intersection — hours ALL models agree on)

#### Scenario: COP minimum across models
- **WHEN** models produce COP estimates [3.2, 2.8, 3.5, 2.6]
- **THEN** CopEstimateMin=2.6

### Requirement: Envelope fields appear in discovery as additional sensor components
Discovery payloads for indices and energy devices SHALL register additional sensor components for each envelope field (`_min`, `_max`, `_confidence` for indices; `_max`, `_min`, `_conservative` for energy). They SHALL share the same state topic as the base fields and use `value_template` to extract the specific JSON key.

#### Scenario: Indices device discovery includes envelope
- **WHEN** BuildDiscoveryPayload is called for the indices device
- **THEN** the payload contains components for `outdoor`, `outdoor_min`, `outdoor_max`, `outdoor_confidence` (and likewise for all other score fields)

#### Scenario: Energy device discovery includes envelope
- **WHEN** BuildDiscoveryPayload is called for the energy device
- **THEN** the payload contains components for `heating_demand_max`, `cop_estimate_min`, `cop_optimal_conservative`

### Requirement: Envelope fields appear in state payloads alongside existing values
State JSON for indices and energy SHALL include envelope fields at the same level as existing fields. Existing field names and values SHALL NOT change.

#### Scenario: Indices state payload
- **WHEN** index result is serialized to state JSON
- **THEN** the JSON contains both `"outdoor": 72` and `"outdoor_min": 65, "outdoor_max": 80, "outdoor_confidence": 0.8`

#### Scenario: Energy state payload
- **WHEN** energy result is serialized to state JSON
- **THEN** the JSON contains both `"heating_demand": 45` and `"heating_demand_max": 62, "cop_estimate_min": 2.6`
