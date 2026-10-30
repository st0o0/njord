# Tasks

## 1. Remove EnsembleParams.Models and old ModelComparison

- [ ] 1.1 Remove `Models = 1L << 3` from `EnsembleParams` enum (`src/Njord.Core/Forecast/EnsembleParams.cs`)
- [ ] 1.2 Remove `Models => true` case from `EnsemblePoint.HasFields` (`src/Njord.Core/Forecast/EnsemblePoint.cs`)
- [ ] 1.3 Remove `Models` filter from `EnsembleApiVariableMappingExtensions.ToApiNames` (`src/Njord.OpenMeteo/Ensemble/EnsembleApiVariableMappingExtensions.cs`)
- [ ] 1.4 Delete `ModelComparison` feature folder (`src/Njord.Features/Forecasts/ModelComparison/`)
- [ ] 1.5 Delete `ModelComparisonInput` from `FeatureInputs.cs` (`src/Njord.Core/Domain/FeatureInputs.cs`)
- [ ] 1.6 Delete `ModelComparisonResult`, `ModelComparisonDay`, `ModelDayValues` from `ForecastResults.cs` -- keep `ModelDayValues` and `ModelComparisonDay` (reused by new features)
- [ ] 1.7 Remove `ModelComparison` registration from `ServiceCollectionExtensions.AddFeatures` (`src/Njord.Features/ServiceCollectionExtensions.cs`)
- [ ] 1.8 Delete `ModelComparisonTests` (`src/Njord.Features.Tests/Forecasts/ModelComparison/`)
- [ ] 1.9 Update `DomainRequirementsTests` to remove `EnsembleParams.Models` references (`src/Njord.Core.Tests/Domain/DomainRequirementsTests.cs`)
- [ ] 1.10 Update `EnsembleParamsTests` (`src/Njord.Core.Tests/Forecast/EnsembleParamsTests.cs`)

## 2. Make EnsembleClient config-driven

- [ ] 2.1 Replace `_needsModels` flag with `_hasMultipleModels = configured.Length > 1` in `EnsembleClient` (`src/Njord.OpenMeteo/Ensemble/EnsembleClient.cs`)
- [ ] 2.2 Update `EnsembleOptions` default to `["ecmwf_ifs025_ensemble_mean", "gfs025_ensemble_mean"]` (`src/Njord.OpenMeteo/Configuration/EnsembleOptions.cs`)
- [ ] 2.3 Update `BudgetGuard.EnsembleWeight` to use `config.EnsembleModelCount > 1` instead of flag check (`src/Njord.Polling/Configuration/BudgetGuard.cs`)
- [ ] 2.4 Update `BudgetGuardTests` (`src/Njord.Polling.Tests/BudgetGuardTests.cs`)
- [ ] 2.5 Update `OpenMeteoClientTests` for ensemble tests referencing `EnsembleParams.Models` (`src/Njord.OpenMeteo.Tests/OpenMeteoClientTests.cs`)

## 3. Split LocationForecast model dictionaries

- [ ] 3.1 Replace `ModelForecast` with `ForecastModels` and `EnsembleModels` on `LocationForecast` (`src/Njord.Core/Domain/LocationForecast.cs`). Replace `Models` property and `WithModel` with `WithForecastModel` / `WithEnsembleModel`.
- [ ] 3.2 Update `FeatureEngine` to route by `Domain` + `ModelId`: Forecast models -> `WithForecastModel`, Ensemble models -> `WithEnsembleModel`. Carry both forward on domain push. (`src/Njord.Pipeline/Stream/FeatureEngine.cs`)
- [ ] 3.3 Update `FeatureEngine` to evaluate features with `requirements == default` on every domain push (`src/Njord.Pipeline/Stream/FeatureEngine.cs`)
- [ ] 3.4 Update `FeatureEngineTests` (`src/Njord.Pipeline.Tests/Stream/FeatureEngineTests.cs`)

## 4. Add ForecastModelComparison and EnsembleModelComparison features

- [ ] 4.1 Add `ForecastModelsInput` and `EnsembleModelsInput` to `FeatureInputs.cs` (`src/Njord.Core/Domain/FeatureInputs.cs`)
- [ ] 4.2 Add `ForecastModelComparisonResult` and `EnsembleModelComparisonResult` to `ForecastResults.cs` (`src/Njord.Messages/Results/Forecasts/ForecastResults.cs`). Reuse `ModelComparisonDay` and `ModelDayValues`.
- [ ] 4.3 Create `ForecastModelComparison` feature (`src/Njord.Features/Forecasts/ForecastModelComparison/`): `ForecastModelComparison.cs` + `ForecastModelComparisonConfig.cs`. Inject `IOptions<ForecastOptions>`, Enabled = config.Enabled && forecastOptions.Models.Length >= 2. Requirements = default. Project from `LocationForecast.ForecastModels`.
- [ ] 4.4 Create `EnsembleModelComparison` feature (`src/Njord.Features/Forecasts/EnsembleModelComparison/`): `EnsembleModelComparison.cs` + `EnsembleModelComparisonConfig.cs`. Inject `IOptions<EnsembleOptions>`, Enabled = config.Enabled && ensembleOptions.Models.Length >= 2. Requirements = default. Project from `LocationForecast.EnsembleModels`.
- [ ] 4.5 Register both in `ServiceCollectionExtensions.AddFeatures`
- [ ] 4.6 Add `ForecastModelComparisonTests` and `EnsembleModelComparisonTests`

## 5. BaseParams cleanup

- [ ] 5.1 Remove `Pm10` addition from `DomainRequirements.WithBaseParams` (`src/Njord.Core/Domain/DomainRequirements.cs`)
- [ ] 5.2 Update `DomainRequirementsTests` for the WithBaseParams change

## 6. Proto and gRPC

- [ ] 6.1 Update `weather.proto`: reserve field 91 and name `model_comparison`. Add `ForecastModelComparison` (field 93) and `EnsembleModelComparison` (field 94) oneof cases. Add wrapper messages. Remove old `ModelComparison` message.
- [ ] 6.2 Update `EnrichmentEventMappingExtensions` for new result types (`src/Njord.Grpc/Mapping/EnrichmentEventMappingExtensions.cs`)
- [ ] 6.3 Update `FeatureResultMappingExtensions` for new `ToProto` methods (`src/Njord.Grpc/Mapping/FeatureResultMappingExtensions.cs`)
- [ ] 6.4 Update `EnrichmentEventMappingsTests` and related gRPC tests

## 7. Docs and config

- [ ] 7.1 Update `docs/data/features.json`: remove `ModelComparison`, add `ForecastModelComparison` and `EnsembleModelComparison` (default-on)
- [ ] 7.2 Update Config Builder (`docs/.vitepress/theme/builder/`) for new features and default ensemble models
- [ ] 7.3 Update `appsettings.Example.json` and `docker-compose.example.yml` with new defaults
- [ ] 7.4 Update `docs/configuration/model-comparison.md` for the two new features

## 8. ha-njord

- [ ] 8.1 Update proto files and regenerate `weather_pb2.py`
- [ ] 8.2 Replace `Feature.MODEL_COMPARISON` with `Feature.FORECAST_MODEL_COMPARISON` and `Feature.ENSEMBLE_MODEL_COMPARISON` in `const.py`
- [ ] 8.3 Update `FEATURE_KEYS` mapping in `const.py`
- [ ] 8.4 Update `grpc_client.py` enrichment event conversion for two new oneof cases
- [ ] 8.5 Update `lifestyle.py`: replace single sensor with two sensors ("Forecast Model Spread", "Ensemble Model Spread")
- [ ] 8.6 Update translations (`en.json`, `de.json`, `strings.json`)
- [ ] 8.7 Update `test_lifestyle.py` for two sensors
- [ ] 8.8 Update `test_grpc_client.py` for new enrichment conversion
- [ ] 8.9 Update `test_proto_import.py` for new message names

## 9. Integration and cross-cutting

- [ ] 9.1 Update `HandlerRequirementsTests` (`src/Njord.IntegrationTests/HandlerRequirementsTests.cs`)
- [ ] 9.2 Update `PipelineEventMappingsTests` if `ForecastDataReceived.ModelId` routing changed
- [ ] 9.3 Update E2E test plan and docker-compose configs for new default models
- [ ] 9.4 Update `PollingOptionsValidatorTests` if ensemble model references changed
- [ ] 9.5 Run all test projects, verify `dotnet format`
