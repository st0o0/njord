## Why

The weather domain currently routes all requests through the Open-Meteo Ensemble Mean API (`ensemble-api.open-meteo.com/v1/ensemble`). This conflates two distinct concerns: deterministic forecasting (what will happen) and ensemble uncertainty (how confident are we). The Ensemble Mean API delivers lower geographic resolution (~25 km), lacks working `uv_index` data, has no `precipitation_probability`, and its mean values smooth out the extremes that alerts need to detect. The deterministic Forecast API (`api.open-meteo.com/v1/forecast`) provides higher resolution (1-9 km via best_match model blending), native `precipitation_probability`, working `uv_index`, and sharper signal for threshold-based alerts. Splitting the two lets each API serve its natural purpose.

## What Changes

- **New `DomainKind.Ensemble`** with its own poll cycle, call weight, and failure backoff -- a first-class domain alongside Weather, AirQuality, Flood, Marine, Seasonal, Satellite.
- **New `ForecastClient`** (`api.open-meteo.com/v1/forecast`, `DomainKind.Weather`) replaces the current `WeatherClient` as the primary weather data source. Uses best_match (automatic high-resolution model blending), no `models` parameter. Deterministic single-value forecast.
- **New `EnsembleClient`** (`ensemble-api.open-meteo.com/v1/ensemble`, `DomainKind.Ensemble`) delivers spread data for uncertainty/confidence and full weather data per comparison model.
- **New `EnsemblePoint`** in `Njord.Forecast` -- a slim point type carrying only spread fields (`TemperatureSpread`, `PrecipitationSpread`, `WindSpeedSpread`).
- **New `EnsembleParams`** flags enum in `Njord.Forecast` for ensemble domain requirements.
- **`EnsembleStdDev`, `PrecipitationSpread`, `WindSpeedSpread` removed from `WeatherPoint`** -- **BREAKING** for persistence records and gRPC wire format.
- **`WeatherClient` removed**, replaced by `ForecastClient` and `EnsembleClient`.
- **Alert confidence source changes**: alerts that need confidence require `DomainKind.Ensemble` in their `DomainRequirements`. Alerts access spread via `LocationForecast.Ensemble` instead of `WeatherPoint.EnsembleStdDev`.
  - Require Ensemble (for confidence): Frost, Heat, Storm, Thunderstorm, Snow, Ice, HeavyRain, WindChill, TropicalNight.
  - No Ensemble (confidence always Medium): PressureDrop, Fog, Visibility, UV, Humidity.
- **`ConfidenceTrend`** requires `Ensemble` (its sole data source).
- **`ModelComparison`** requires `Ensemble` (comparison models live there now).
- **`OpenMeteoEndpoints`** gains `ForecastHost` (`api.open-meteo.com`) and `ForecastPath` (`v1/forecast`); existing `WeatherHost`/`WeatherPath` renamed to `EnsembleHost`/`EnsemblePath`.

## Capabilities

### New Capabilities
- `ensemble-domain`: New `DomainKind.Ensemble` with `EnsembleClient`, `EnsemblePoint`, `EnsembleParams`, spread-only polling, and comparison model delivery.
- `forecast-client`: New `ForecastClient` as primary weather data source using the deterministic Forecast API with best_match model blending.

### Modified Capabilities
- `domain-point-types`: `WeatherPoint` loses spread fields; new `EnsemblePoint` added.
- `domain-requirements`: `DomainRequirements` gains `Ensemble: EnsembleParams` slot.
- `domain-clients`: `WeatherClient` replaced by `ForecastClient`; `EnsembleClient` registered as new `IDomainClient`.
- `feature-rule-abstraction`: Alert rules access confidence from `LocationForecast.Ensemble` instead of `WeatherPoint.EnsembleStdDev`.

## Impact

- **Njord.Forecast**: new `EnsemblePoint`, `EnsembleParams`; `WeatherPoint` loses 3 fields; `DomainKind` gains `Ensemble`.
- **Njord.Core**: `DomainRequirements` gains `Ensemble` slot; `LocationForecast` gains `Ensemble` property; `FeatureResolution` resolves ensemble requirements; `FeatureContext`/`FeatureInputs` carry ensemble data.
- **Njord.OpenMeteo**: `WeatherClient` split into `ForecastClient` + `EnsembleClient`; `OpenMeteoEndpoints` updated; `OpenMeteoSetupContainer` registers both; response mapping shared.
- **Njord.Features**: 9 alerts add `Ensemble` to requirements and read spread from `LocationForecast`; 5 alerts drop `EnsembleStdDev` from requirements; `ConfidenceTrend` and `ModelComparison` require `Ensemble`.
- **Njord.Persistence**: **BREAKING** -- persistence records for weather drop spread fields.
- **Njord.Grpc**: proto mapping updated for ensemble domain and spread removal from weather.
- **Njord.Polling**: `ShardMessageExtractor` and `BudgetGuard` handle new domain; `PollWorker` gets ensemble timer.
- **protos/**: `weather.proto` updated -- spread fields removed from weather sample, ensemble data added.
- **E2E compose**: weather host changes, ensemble domain configuration added.
- **docs/data/features.json** and Config Builder: updated for ensemble domain and per-feature requirements.

## Budget Impact

Per location, per poll cycle (free tier: 300k/month, 10k/day):

| Configuration | Today | After change |
|---|---|---|
| Alerts only (no ModelComparison) | ~8 calls (ensemble, 2 models) | ~5 calls (4 forecast + 1 ensemble spread) |
| All features, 3 comparison models | ~8 calls (ensemble, 2 models) | ~17 calls (4 forecast + 1 spread + 12 comparison) |
| All features, 1 comparison model | ~4 calls (ensemble, 1 model) | ~9 calls (4 forecast + 1 spread + 4 comparison) |

The common case (alerts without model comparison) is cheaper. Full model comparison is more expensive because the forecast API call is additional, but delivers higher-quality primary data.

## Non-goals

- Adding new weather variables or features beyond what exists today.
- Changing the polling architecture (PollWorker per-domain timers, discovery cycle).
- Supporting the full Ensemble API (individual members) -- only ensemble-mean is used.
- Changing the gRPC service contract beyond the weather/ensemble field restructuring.
- Hot-reloading configuration or runtime model switching.
