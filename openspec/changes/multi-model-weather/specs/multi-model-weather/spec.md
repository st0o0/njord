## ADDED Requirements

### Requirement: Model selection configuration
The system SHALL read an ordered list of weather model ids from `Njord:Weather:Models`, defaulting to `ecmwf_ifs025_ensemble_mean`. The first entry SHALL be the primary model. The single-value `Njord:Weather:Model` SHALL no longer be accepted.

#### Scenario: Default configuration
- **WHEN** no models are configured
- **THEN** the effective model list is `ecmwf_ifs025_ensemble_mean` and it is the primary model

#### Scenario: Invalid list rejected at startup
- **WHEN** `Models` is empty, contains a duplicate, or contains an id missing from the model catalog
- **THEN** startup validation fails and the message names the offending entry

#### Scenario: Legacy key
- **WHEN** `Njord:Weather:Model` is set
- **THEN** startup validation fails and the message points to `Njord:Weather:Models`

### Requirement: ModelComparison feature gate
The system SHALL fetch more than the primary model only when the `ModelComparison` feature is enabled; it SHALL be off by default.

#### Scenario: Feature off
- **WHEN** `Models` lists three models and `ModelComparison` is disabled
- **THEN** only the primary model is requested and one startup warning names the ignored models

#### Scenario: Feature on
- **WHEN** `ModelComparison` is enabled with three models
- **THEN** all three models are requested

### Requirement: Single batched weather request
The system SHALL request all effective models in one HTTP call per location and poll cycle, using a comma-separated `models` parameter.

#### Scenario: Three models
- **WHEN** a weather fetch runs with three effective models
- **THEN** exactly one request to `v1/ensemble` is sent and its `models` parameter lists the three ids in configured order

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
The weather call weight SHALL be `ceil(variables / 10)` multiplied by the number of effective models. Startup validation, the budget health check and the Config Builder SHALL use this weight.

#### Scenario: Weight with three models
- **WHEN** 31 weather variables and three effective models are configured
- **THEN** the weather weight is 12 calls per poll

#### Scenario: Budget gate
- **WHEN** projected usage exceeds 80 % of the monthly budget because of added models
- **THEN** startup validation fails with the projected figure
