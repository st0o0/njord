# passive-model-comparison Specification

## Purpose
Two passive model-comparison features that observe whatever models the config fetches, without driving multi-model fetching themselves.

## Requirements

### Requirement: EnsembleClient is config-driven
The `EnsembleClient` SHALL decide multi-model mode from the configured model count, not from a feature flag. When multiple models are configured, the client SHALL request all of them and include weather variables alongside spread variables. When only one model is configured, it SHALL request spread variables only.

#### Scenario: Two ensemble models configured
- **GIVEN** `Njord:Ensemble:Models` contains two models
- **WHEN** the `EnsembleClient` is constructed
- **THEN** it SHALL request both models and include weather variables from `AllForecast`

#### Scenario: One ensemble model configured
- **GIVEN** `Njord:Ensemble:Models` contains one model
- **WHEN** the `EnsembleClient` is constructed
- **THEN** it SHALL request only that model with spread variables only

### Requirement: Default ensemble models include three models
The default value for `Njord:Ensemble:Models` SHALL be `["ecmwf_ifs025_ensemble_mean", "ncep_gefs025_ensemble_mean", "google_weathernext2_ensemble_mean"]`, enabling model comparison out of the box.

#### Scenario: No models configured
- **WHEN** `Njord:Ensemble:Models` is not set
- **THEN** the effective model list SHALL be `["ecmwf_ifs025_ensemble_mean", "ncep_gefs025_ensemble_mean", "google_weathernext2_ensemble_mean"]`

### Requirement: LocationForecast has separate model dictionaries
`LocationForecast` SHALL have `ForecastModels` (from Forecast API) and `EnsembleModels` (from Ensemble API) as separate dictionaries.

#### Scenario: Forecast model data routed correctly
- **GIVEN** a `DomainDataReceived` with `Domain == Forecast` and `ModelId` set
- **WHEN** `FeatureEngine` processes it
- **THEN** the data SHALL appear in `LocationForecast.ForecastModels`

#### Scenario: Ensemble model data routed correctly
- **GIVEN** a `DomainDataReceived` with `Domain == Ensemble` and `ModelId` set
- **WHEN** `FeatureEngine` processes it
- **THEN** the data SHALL appear in `LocationForecast.EnsembleModels`

### Requirement: Features with no requirements are always evaluated
A feature whose `Descriptor.Requirements` requires no domain SHALL be evaluated on every domain push. The feature's `Project` method is the filter.

#### Scenario: Passive feature evaluated on Forecast push
- **GIVEN** `EnsembleModelComparison` is enabled with no domain requirements
- **WHEN** a Forecast domain push arrives
- **THEN** `EnsembleModelComparison.Project` SHALL be called

#### Scenario: Passive feature skips when no models
- **GIVEN** `EnsembleModelComparison` is enabled but `EnsembleModels` is empty
- **WHEN** any domain push arrives
- **THEN** `Project` SHALL return null and no result is emitted

### Requirement: ForecastModelComparison feature
`ForecastModelComparison` SHALL compare per-model forecasts from the Forecast API. It SHALL be default-on. It SHALL have no domain requirements.

#### Scenario: Two forecast models available
- **GIVEN** `ForecastModels` contains two models with temperature and precipitation data
- **WHEN** a domain push triggers evaluation
- **THEN** a `ForecastModelComparisonResult` SHALL be emitted with daily temp/precip per model for 5 days

#### Scenario: No forecast models configured
- **GIVEN** `Njord:Forecast:Models` is empty (best_match)
- **THEN** the feature SHALL emit nothing (Project returns null)

### Requirement: EnsembleModelComparison feature
`EnsembleModelComparison` SHALL compare per-model forecasts from the Ensemble API. It SHALL be default-on. It SHALL have no domain requirements.

#### Scenario: Two ensemble models available
- **GIVEN** `EnsembleModels` contains two models with temperature data
- **WHEN** an Ensemble domain push triggers evaluation
- **THEN** an `EnsembleModelComparisonResult` SHALL be emitted with daily temp/precip per model for 5 days

#### Scenario: Default config enables EnsembleModelComparison
- **GIVEN** no explicit model or feature config
- **THEN** `EnsembleModelComparison` SHALL be enabled and emitting results (2 default models)

### Requirement: BudgetGuard is config-driven
`BudgetGuard` SHALL calculate ensemble weight using the configured model count directly. When multiple models are configured, weather variables SHALL be included in the variable count.

#### Scenario: Budget with 3 ensemble models
- **GIVEN** 3 ensemble models, 15 forecast variables, 3 spread variables, 4 ensemble days
- **THEN** ensemble weight SHALL be `ceil((15+3)/10) * (4/14) * 3`

#### Scenario: Budget with 1 ensemble model
- **GIVEN** 1 ensemble model, 3 spread variables, 4 ensemble days
- **THEN** ensemble weight SHALL be `ceil(3/10) * (4/14) * 1`

### Requirement: BaseParams does not add Pm10
`WithBaseParams` SHALL NOT add `AirQualityParams.Pm10`. Only `WeatherCode` SHALL be added for Forecast.

#### Scenario: WithBaseParams does not add Pm10
- **GIVEN** enabled features that require AirQuality for pollen only
- **WHEN** `WithBaseParams` is called
- **THEN** `Pm10` SHALL NOT be in the result

### Requirement: Proto has two enrichment event types
`EnrichmentEvent` SHALL have oneof cases `forecast_model_comparison` and `ensemble_model_comparison`. Both messages SHALL reuse `ModelComparisonDay` and `ModelDayValues`.

#### Scenario: Ensemble comparison event
- **GIVEN** an `EnsembleModelComparisonResult` with 2 models over 3 days
- **WHEN** mapped to proto
- **THEN** the `ensemble_model_comparison` field SHALL contain 3 `ModelComparisonDay` entries each with 2 `ModelDayValues`

### Requirement: ha-njord exposes two sensors
ha-njord SHALL expose `FORECAST_MODEL_COMPARISON` and `ENSEMBLE_MODEL_COMPARISON` features. Each SHALL create a sensor showing the temperature spread (max - min of day 0 highs).

#### Scenario: Ensemble model spread sensor
- **GIVEN** an `ensemble_model_comparison` event with day 0 models having max temperatures 21.4 and 19.0
- **THEN** the "Ensemble Model Spread" sensor SHALL show 2.4

#### Scenario: Sensor hidden when enrichment inactive
- **GIVEN** the server's active enrichments do not contain the enrichment key
- **THEN** the sensor SHALL not be created
