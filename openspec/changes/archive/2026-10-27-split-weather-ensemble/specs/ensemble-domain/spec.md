## ADDED Requirements

### Requirement: Ensemble is a first-class domain
The system SHALL treat Ensemble as its own `DomainKind` with an independent poll cycle, call weight, failure backoff, and discovery phase -- identical in lifecycle to Weather, AirQuality, Flood, Marine, Seasonal, and Satellite.

#### Scenario: Ensemble polls independently
- **WHEN** the Weather domain completes a poll cycle
- **THEN** the Ensemble domain's next poll time SHALL NOT be affected

#### Scenario: Ensemble failure does not block Weather
- **WHEN** the Ensemble API returns an error
- **THEN** the Weather domain SHALL continue polling on its own schedule

### Requirement: EnsembleClient fetches from the Ensemble Mean API
The `EnsembleClient` SHALL be an `IDomainClient` with `Domain = DomainKind.Ensemble`. It SHALL fetch from `ensemble-api.open-meteo.com/v1/ensemble` with the `models` parameter set to the resolved ensemble model ids.

#### Scenario: Spread-only fetch
- **WHEN** only spread-requiring features are enabled (no ModelComparison)
- **THEN** the EnsembleClient SHALL request only the spread variables for a single ensemble-mean model

#### Scenario: Spread plus comparison models
- **WHEN** ModelComparison is enabled alongside spread-requiring features
- **THEN** the EnsembleClient SHALL request the full weather variable set for all configured ensemble-mean models, plus the spread variables

### Requirement: EnsembleClient delivers spread as primary and comparison models in Models
The `EnsembleClient`'s `FetchSuccess` SHALL carry an `EnsemblePoint` array as its primary forecast (spread data from the first configured ensemble-mean model) and the per-model `WeatherPoint` forecasts in `FetchSuccess.Models`.

#### Scenario: Primary forecast is EnsemblePoint
- **WHEN** the EnsembleClient returns a successful fetch
- **THEN** `FetchSuccess.Forecast` SHALL be a `DomainForecast<EnsemblePoint>`

#### Scenario: Comparison models in Models dictionary
- **WHEN** multiple ensemble-mean models are configured and ModelComparison is enabled
- **THEN** `FetchSuccess.Models` SHALL contain one `DomainForecast<WeatherPoint>` per model, keyed by model id

### Requirement: Ensemble call weight
The EnsembleClient SHALL report a `CallWeight` of `ceil(variables / 10) * ceil(forecastDays / 14) * modelCount`. When only spread variables are requested (3 variables), the weight for a single model with up to 14 forecast days SHALL be 1.

#### Scenario: Spread-only weight
- **WHEN** 3 spread variables are requested for 1 model with 4 forecast days
- **THEN** `CallWeight` SHALL be 1

#### Scenario: Full comparison weight
- **WHEN** 31 variables are requested for 3 models with 4 forecast days
- **THEN** `CallWeight` SHALL be 12

### Requirement: Ensemble domain is polled only when required
The Ensemble domain SHALL be polled only when at least one enabled feature lists `Ensemble` in its `DomainRequirements`. When no enabled feature requires Ensemble, the domain SHALL NOT be fetched and SHALL NOT consume budget.

#### Scenario: No ensemble features enabled
- **WHEN** only PressureDrop and Fog alerts are enabled
- **THEN** the Ensemble domain SHALL NOT be polled

#### Scenario: Frost alert enables ensemble
- **WHEN** the Frost alert is enabled
- **THEN** the Ensemble domain SHALL be polled because Frost requires ensemble spread for confidence
