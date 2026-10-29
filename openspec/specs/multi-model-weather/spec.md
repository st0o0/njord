# multi-model-weather Specification

## Purpose
Fetch, serve and compare several Open-Meteo ensemble-mean weather models per location in one batched request; the primary model drives enrichments.

## Requirements

### Requirement: Model selection configuration
The system SHALL read ensemble model ids from `Njord:Ensemble:Models`, defaulting to `[ecmwf_ifs025_ensemble_mean]`. The first entry SHALL be the primary model. No model catalog SHALL exist; validation SHALL reject empty lists and duplicates but SHALL NOT reject unknown model ids.

#### Scenario: Default configuration
- **WHEN** no models are configured in `Njord:Ensemble`
- **THEN** the effective model list is `[ecmwf_ifs025_ensemble_mean]` and it is the primary model

#### Scenario: Invalid list rejected at startup
- **WHEN** `Njord:Ensemble:Models` is empty or contains a duplicate
- **THEN** startup validation SHALL fail and the message names the problem

#### Scenario: Unknown model id accepted
- **WHEN** `Njord:Ensemble:Models` contains a model id not in any hardcoded catalog
- **THEN** startup validation SHALL succeed; the API will return an error at fetch time if the id is invalid

### Requirement: ModelComparison feature gate
The system SHALL fetch more than the primary model only when `EnsembleParams.Models` is set in the resolved domain requirements (indicating a feature needs per-model data). The `EnsembleClient` SHALL determine this from the flag, not from a feature-name string match.

#### Scenario: Feature off
- **WHEN** `Njord:Ensemble:Models` lists three models and no enabled feature sets `EnsembleParams.Models`
- **THEN** only the primary model is requested and a startup warning names the ignored models

#### Scenario: Feature on
- **WHEN** an enabled feature sets `EnsembleParams.Models` with three models configured
- **THEN** all three models are requested

### Requirement: Single batched weather request
The system SHALL request all effective models in one HTTP call per location and poll cycle, using a comma-separated `models` parameter.

#### Scenario: Three models
- **WHEN** an ensemble fetch runs with three effective models
- **THEN** exactly one request to the ensemble endpoint is sent and its `models` parameter lists the three ids in configured order

### Requirement: Per-model forecasts
The system SHALL turn one multi-model response into one weather forecast per model, each with the same point structure as today.

#### Scenario: All models answer
- **WHEN** the response contains values for every requested model
- **THEN** the pipeline receives one weather forecast per model, tagged with its model id

#### Scenario: One model has no data
- **WHEN** one requested model returns only null values
- **THEN** the other models are delivered and the empty model is omitted without failing the fetch

#### Scenario: Single-model response
- **WHEN** only one model is requested
- **THEN** the unsuffixed response shape is mapped to that model's forecast

### Requirement: Primary model drives enrichments
Feature rules SHALL evaluate only the primary model's forecast. Secondary models SHALL be stored and served but SHALL NOT trigger enrichment evaluation.

#### Scenario: Secondary model updates
- **WHEN** only a secondary model's data changes
- **THEN** the model's data is available to consumers and no enrichment event is emitted

### Requirement: Budget accounting per model
The ensemble call weight SHALL be `ceil(variables / 10) * ceil(EnsembleForecastDays / 14)` multiplied by the number of effective models, using the Ensemble section's own ForecastDays.

#### Scenario: Weight with three models
- **WHEN** 31 variables and three effective models are configured with Ensemble ForecastDays 15
- **THEN** the ensemble weight is `ceil(31/10) * ceil(15/14) * 3 = 4 * 2 * 3 = 24` calls per poll
