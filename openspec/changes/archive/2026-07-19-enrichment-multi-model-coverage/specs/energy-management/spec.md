## MODIFIED Requirements

### Requirement: EnergyResult includes pessimistic envelope fields
`EnergyResult` SHALL include additional fields: `HeatingDemandMax` (int), `CopEstimateMin` (double?), and `CopOptimalConservative` (IReadOnlyList of hours). These represent the worst-case scenario across all models for use in conservative automation decisions.

#### Scenario: Heating demand worst case
- **WHEN** 4 models produce per-model heating demand values [40, 55, 45, 62]
- **THEN** HeatingDemandMax=62 and HeatingDemand remains the median/mean-based value

#### Scenario: COP minimum
- **WHEN** 3 models produce COP estimates [3.2, 2.8, 3.5]
- **THEN** CopEstimateMin=2.8

#### Scenario: Conservative optimal hours (intersection)
- **WHEN** model A COP optimal = [2,3,4,5], model B = [3,4,5,6], model C = [4,5]
- **THEN** CopOptimalConservative = [4,5]

#### Scenario: No models provide COP data
- **WHEN** temperature parameter is not available
- **THEN** CopEstimateMin is null and CopOptimalConservative is empty

### Requirement: Energy computation evaluates each model independently then aggregates
`EnergyResult.Compute` SHALL first compute a full energy result per model (HeatingDemand, CopEstimate, CopOptimal, Shading, NightCooling) using only that model's forecast data. It SHALL then: keep existing mean-based values as the primary output, derive envelope fields from the per-model results (max of HeatingDemand, min of CopEstimate, intersection of CopOptimal hours).

#### Scenario: Per-model computation isolation
- **WHEN** model A has temp=5°C mean and model B has temp=-2°C mean
- **THEN** each model's heating demand is computed independently (model B will be higher), and HeatingDemandMax reflects model B's value

#### Scenario: Single model fallback
- **WHEN** only 1 model provides data
- **THEN** envelope fields equal the primary values (HeatingDemandMax = HeatingDemand, CopEstimateMin = CopEstimate)

### Requirement: State payload includes envelope fields
The energy state JSON SHALL include `heating_demand_max`, `cop_estimate_min`, and `cop_optimal_conservative` alongside existing fields. Existing field names and semantics SHALL NOT change.

#### Scenario: JSON structure
- **WHEN** energy result is serialized
- **THEN** JSON contains `{"heating_demand": 45, "heating_demand_max": 62, "cop_estimate": 3.2, "cop_estimate_min": 2.6, "cop_optimal": [...], "cop_optimal_conservative": [...], ...}`
