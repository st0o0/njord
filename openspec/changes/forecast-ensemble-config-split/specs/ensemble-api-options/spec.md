## ADDED Requirements

### Requirement: Independent Ensemble API configuration section
The system SHALL read Ensemble API settings from the `Njord:Ensemble` configuration section. The section SHALL contain `ForecastDays` (integer, default 4, range 1..35) and `Models` (string array, required, at least one entry, default `[ecmwf_ifs025_ensemble_mean]`).

#### Scenario: Defaults
- **WHEN** `Njord:Ensemble` is not configured
- **THEN** the Ensemble API client SHALL use ForecastDays=4 and Models=`[ecmwf_ifs025_ensemble_mean]`

#### Scenario: Custom ForecastDays
- **WHEN** `Njord:Ensemble:ForecastDays` is 15
- **THEN** the Ensemble API request SHALL use `forecast_days=15`, independent of the Forecast API setting

#### Scenario: ForecastDays above limit
- **WHEN** `Njord:Ensemble:ForecastDays` is 36
- **THEN** startup validation SHALL fail naming the setting and its maximum of 35

#### Scenario: Models required
- **WHEN** `Njord:Ensemble:Models` is explicitly set to an empty array
- **THEN** startup validation SHALL fail with a message stating at least one model is required

#### Scenario: Duplicate models rejected
- **WHEN** `Njord:Ensemble:Models` contains a duplicate entry
- **THEN** startup validation SHALL fail naming the duplicate

### Requirement: Ensemble client reads models from its own options
The Ensemble API client SHALL read the model list from `Njord:Ensemble:Models`. When the `EnsembleParams.Models` flag is set in the resolved requirements (indicating a feature needs per-model data), the client SHALL request all configured models. When the flag is not set, the client SHALL request only the first model.

#### Scenario: Multi-model mode
- **WHEN** `EnsembleParams.Models` is set and three models are configured
- **THEN** the Ensemble API request SHALL include all three models and return per-model forecasts

#### Scenario: Spread-only mode
- **WHEN** `EnsembleParams.Models` is not set and three models are configured
- **THEN** the Ensemble API request SHALL include only the first model and return only spread data

#### Scenario: Ignored models logged
- **WHEN** three models are configured and only the first is used
- **THEN** a startup warning SHALL name the two ignored models

### Requirement: Ensemble API budget accounting with independent ForecastDays
The Ensemble API call weight SHALL use its own ForecastDays (not the Forecast API value) in the formula `ceil(variables / 10) * ceil(ForecastDays / 14) * modelCount`.

#### Scenario: Independent ForecastDays in budget
- **WHEN** Forecast ForecastDays is 4 and Ensemble ForecastDays is 15 with 1 model and 5 ensemble variables
- **THEN** the Ensemble call weight SHALL be `ceil(5/10) * ceil(15/14) * 1 = 2`, not `ceil(5/10) * ceil(4/14) * 1 = 1`
