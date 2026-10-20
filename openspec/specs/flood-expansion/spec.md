# flood-expansion Specification

## Purpose
TBD - created by archiving change expand-flood. Update Purpose after archive.
## Requirements
### Requirement: Ensemble members are optional
`FloodOptions.IncludeEnsemble` (default false) SHALL control whether the flood request contains `ensemble=true`. When true, the response mapping SHALL expose the per-day member discharges on `FloodPoint.EnsembleMembers`; when false the request and points SHALL be identical to the behavior before this change.

#### Scenario: Ensemble requested
- **WHEN** `IncludeEnsemble` is true
- **THEN** the flood request URL SHALL contain `ensemble=true` and each mapped `FloodPoint` SHALL carry its member discharges

#### Scenario: Default request
- **WHEN** `IncludeEnsemble` is not configured
- **THEN** the flood request URL SHALL NOT contain an `ensemble` parameter and `EnsembleMembers` SHALL be null

#### Scenario: Missing member values
- **WHEN** some member series contain null for a day
- **THEN** those entries SHALL be omitted from that day's member list

### Requirement: Flood statistic flags remain complete
`FloodParams` SHALL keep its statistic flags and add an `Ensemble` flag. Every API-name mapping and `HasFields` switch SHALL have one arm per member and throw `ArgumentOutOfRangeException` for unknown values.

#### Scenario: Ensemble flag maps to the request parameter
- **WHEN** the requested params include `Ensemble`
- **THEN** the request builder SHALL emit `ensemble=true` and SHALL NOT add a daily variable for it

### Requirement: Flood risk confidence from ensemble spread
When ensemble members are present, `FloodRisk` alerts SHALL carry a confidence derived from the share of members exceeding the alert threshold for the evaluated day. Severity SHALL follow the existing rule unchanged. Without members the confidence SHALL be absent and the alert SHALL equal today's alert.

#### Scenario: Most members exceed the threshold
- **WHEN** 45 of 50 members exceed the threshold
- **THEN** the alert SHALL report a high confidence

#### Scenario: Single outlier member
- **WHEN** only 1 of 50 members exceeds the threshold while `river_discharge_max` exceeds it
- **THEN** the alert SHALL report a low confidence

#### Scenario: No ensemble data
- **WHEN** members are not present
- **THEN** the alert SHALL have no confidence value and its severity SHALL equal the pre-change severity

### Requirement: Budget reflects ensemble weight
`BudgetCalculator` SHALL account for the verified flood call weight when `IncludeEnsemble` is true, and the startup guard SHALL use that weight.

#### Scenario: Ensemble raises the projection
- **WHEN** `IncludeEnsemble` is true and the flood weight is above 1
- **THEN** the projected monthly calls SHALL be higher than with `IncludeEnsemble` false for the same locations and interval

#### Scenario: Default unchanged
- **WHEN** `IncludeEnsemble` is false
- **THEN** the projection SHALL equal the projection before this change

### Requirement: Longer forecast horizon stays valid
`FloodOptions.ForecastDays` SHALL accept 1 to 210 with a default of 92.

#### Scenario: Maximum accepted
- **WHEN** `ForecastDays` is 210
- **THEN** options validation SHALL succeed and the request SHALL contain the matching forecast range

#### Scenario: Out of range
- **WHEN** `ForecastDays` is 211
- **THEN** options validation SHALL fail

