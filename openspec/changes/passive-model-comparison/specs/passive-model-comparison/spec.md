# passive-model-comparison Specification

## ADDED Requirements

### Requirement: EnsembleClient is config-driven
The `EnsembleClient` SHALL decide multi-model mode from `EnsembleOptions.Models.Length > 1`, not from a feature flag. When multiple models are configured, the client SHALL request all of them and include weather variables alongside spread variables. When only one model is configured, it SHALL request spread variables only.

#### Scenario: Two ensemble models configured
- **GIVEN** `Njord:Ensemble:Models` contains `["ecmwf_ifs025_ensemble_mean", "gfs025_ensemble_mean"]`
- **WHEN** the `EnsembleClient` is constructed
- **THEN** it SHALL request both models and include weather variables from `AllForecast`

#### Scenario: One ensemble model configured
- **GIVEN** `Njord:Ensemble:Models` contains `["ecmwf_ifs025_ensemble_mean"]`
- **WHEN** the `EnsembleClient` is constructed
- **THEN** it SHALL request only that model with spread variables only

### Requirement: Default ensemble models include two models
The default value for `Njord:Ensemble:Models` SHALL be `["ecmwf_ifs025_ensemble_mean", "gfs025_ensemble_mean"]`, enabling model comparison out of the box.

#### Scenario: No models configured
- **WHEN** `Njord:Ensemble:Models` is not set
- **THEN** the effective model list SHALL be `["ecmwf_ifs025_ensemble_mean", "gfs025_ensemble_mean"]`

### Requirement: LocationForecast has separate model dictionaries
`LocationForecast` SHALL have `ForecastModels` (from Forecast API) and `EnsembleModels` (from Ensemble API) as separate dictionaries. The old unified `ModelForecast` property SHALL NOT exist.

#### Scenario: Forecast model data routed correctly
- **GIVEN** a `DomainDataReceived` with `Domain == Forecast` and `ModelId == "icon_d2"`
- **WHEN** `FeatureEngine` processes it
- **THEN** the data SHALL appear in `LocationForecast.ForecastModels["icon_d2"]`

#### Scenario: Ensemble model data routed correctly
- **GIVEN** a `DomainDataReceived` with `Domain == Ensemble` and `ModelId == "ecmwf_ifs025_ensemble_mean"`
- **WHEN** `FeatureEngine` processes it
- **THEN** the data SHALL appear in `LocationForecast.EnsembleModels["ecmwf_ifs025_ensemble_mean"]`

### Requirement: Features with no requirements are always evaluated
A feature whose `Descriptor.Requirements` equals `default(DomainRequirements)` SHALL be evaluated on every domain push. The feature's `Project` method is the filter.

#### Scenario: Passive feature evaluated on Forecast push
- **GIVEN** `EnsembleModelComparison` is enabled with requirements = default
- **WHEN** a Forecast domain push arrives
- **THEN** `EnsembleModelComparison.Project` SHALL be called

#### Scenario: Passive feature skips when no models
- **GIVEN** `EnsembleModelComparison` is enabled but `EnsembleModels` is empty
- **WHEN** any domain push arrives
- **THEN** `Project` SHALL return null and no result is emitted

### Requirement: ForecastModelComparison feature
`ForecastModelComparison` SHALL compare per-model forecasts from the Forecast API. It SHALL be default-on but auto-disable when `ForecastOptions.Models.Length < 2`. It SHALL have no domain requirements.

#### Scenario: Two forecast models available
- **GIVEN** `ForecastModels` contains models "icon_d2" and "ecmwf_ifs025" with temperature and precipitation data
- **WHEN** a domain push triggers evaluation
- **THEN** a `ForecastModelComparisonResult` SHALL be emitted with daily temp/precip per model for 5 days

#### Scenario: Fewer than two forecast models configured
- **GIVEN** `Njord:Forecast:Models` is empty (best_match)
- **THEN** the feature SHALL be disabled at startup

### Requirement: EnsembleModelComparison feature
`EnsembleModelComparison` SHALL compare per-model forecasts from the Ensemble API. It SHALL be default-on but auto-disable when `EnsembleOptions.Models.Length < 2`. It SHALL have no domain requirements.

#### Scenario: Two ensemble models available
- **GIVEN** `EnsembleModels` contains "ecmwf_ifs025_ensemble_mean" and "gfs025_ensemble_mean" with temperature data
- **WHEN** an Ensemble domain push triggers evaluation
- **THEN** an `EnsembleModelComparisonResult` SHALL be emitted with daily temp/precip per model for 5 days

#### Scenario: Default config enables EnsembleModelComparison
- **GIVEN** no explicit model or feature config
- **THEN** `EnsembleModelComparison` SHALL be enabled (2 default models) and emitting results

### Requirement: BudgetGuard is config-driven
`BudgetGuard` SHALL calculate ensemble weight using `config.EnsembleModelCount` directly. When `modelCount > 1`, weather variables SHALL be included in the variable count. No flag check SHALL exist.

#### Scenario: Budget with 3 ensemble models
- **GIVEN** 3 ensemble models, 15 forecast variables, 3 spread variables, 4 ensemble days
- **THEN** ensemble weight SHALL be `ceil((15+3)/10) * (4/14) * 3 = 2 * 0.29 * 3 = 1.71`

#### Scenario: Budget with 1 ensemble model
- **GIVEN** 1 ensemble model, 3 spread variables, 4 ensemble days
- **THEN** ensemble weight SHALL be `ceil(3/10) * (4/14) * 1 = 0.29`

### Requirement: Proto has two enrichment event types
`EnrichmentEvent` SHALL have oneof cases `forecast_model_comparison` (field 93) and `ensemble_model_comparison` (field 94). Field 91 (`model_comparison`) SHALL be reserved. Both messages SHALL reuse `ModelComparisonDay` and `ModelDayValues`.

#### Scenario: Ensemble comparison event
- **GIVEN** an `EnsembleModelComparisonResult` with 2 models over 3 days
- **WHEN** mapped to proto
- **THEN** the `EnrichmentEvent.ensemble_model_comparison` field SHALL contain 3 `ModelComparisonDay` entries each with 2 `ModelDayValues`

### Requirement: ha-njord exposes two sensors
ha-njord SHALL replace `Feature.MODEL_COMPARISON` with `Feature.FORECAST_MODEL_COMPARISON` and `Feature.ENSEMBLE_MODEL_COMPARISON`. Each SHALL create a sensor showing the temperature spread (max - min of day 0 highs).

#### Scenario: Ensemble model spread sensor
- **GIVEN** an `ensemble_model_comparison` event with day 0 models having max temperatures 21.4 and 19.0
- **THEN** the "Ensemble Model Spread" sensor SHALL show 2.4

#### Scenario: Sensor hidden when enrichment inactive
- **GIVEN** the server's active enrichments do not contain `Forecast:EnsembleModelComparison`
- **THEN** the "Ensemble Model Spread" sensor SHALL not be created

## REMOVED Requirements

### Requirement: EnsembleParams.Models flag
The `EnsembleParams.Models` enum member SHALL be removed. No production code SHALL reference it. The decision to fetch multiple models SHALL be config-driven.

#### Scenario: Flag does not exist
- **THEN** `EnsembleParams` SHALL NOT contain a `Models` member

### Requirement: Old ModelComparison feature
The `ModelComparison` feature, `ModelComparisonConfig`, and `ModelComparisonInput` SHALL be removed.

#### Scenario: Feature does not exist
- **THEN** no feature named `ModelComparison` SHALL exist in `Njord.Features`

### Requirement: BaseParams adds Pm10
`WithBaseParams` SHALL NOT add `AirQualityParams.Pm10`. No feature consumes it.

#### Scenario: WithBaseParams does not add Pm10
- **GIVEN** enabled features that require AirQuality for pollen only
- **WHEN** `WithBaseParams` is called
- **THEN** `AirQualityParams.Pm10` SHALL NOT be in the result

## MODIFIED Requirements

### Requirement: Model selection configuration
The default value for `Njord:Ensemble:Models` SHALL be `["ecmwf_ifs025_ensemble_mean", "gfs025_ensemble_mean"]` instead of `["ecmwf_ifs025_ensemble_mean"]`.

#### Scenario: Default configuration
- **WHEN** no models are configured in `Njord:Ensemble`
- **THEN** the effective model list is `["ecmwf_ifs025_ensemble_mean", "gfs025_ensemble_mean"]` and the first is the primary model
