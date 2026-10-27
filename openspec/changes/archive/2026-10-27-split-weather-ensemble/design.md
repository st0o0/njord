## Context

Today all weather data comes from one source: the Open-Meteo Ensemble Mean API (`ensemble-api.open-meteo.com/v1/ensemble`). A single `WeatherClient` fetches ensemble-mean values and spread data in one call, and `WeatherPoint` carries both deterministic values and `EnsembleStdDev`.

This conflates two concerns:
1. **What will happen** (deterministic forecast -- sharp values, high resolution)
2. **How confident are we** (ensemble spread -- agreement across 51 model members)

The deterministic Forecast API (`api.open-meteo.com/v1/forecast`) offers higher geographic resolution (1-9 km via best_match), native `precipitation_probability`, working `uv_index`, and sharper extremes for alert triggering. The Ensemble Mean API is the right source for uncertainty quantification and model comparison.

## Goals / Non-Goals

**Goals:**
- Separate weather forecasting from ensemble uncertainty into distinct domains
- Use the deterministic Forecast API as the primary weather data source
- Make ensemble spread opt-in per feature through `DomainRequirements`
- Keep the gateway and pipeline abstractions unchanged (1 `IDomainClient` per `DomainKind`)
- Reuse existing response mapping infrastructure (`WeatherResponse`, `SplitByModel`)

**Non-Goals:**
- Adding new weather variables or features
- Supporting full ensemble member data (individual members)
- Changing the polling architecture or discovery cycle
- Hot-reloading models or runtime configuration changes

## Decisions

### D1: New DomainKind.Ensemble instead of composite client

**Decision:** Add `DomainKind.Ensemble` as a seventh domain with its own `EnsembleClient`, poll timer, and call weight.

**Alternatives considered:**
- *Composite WeatherClient* that calls both APIs internally and merges results. Simpler DI, but hides two HTTP calls behind one `IDomainClient`, breaks the 1-call-per-domain assumption in budget tracking, and conflates poll cycles.
- *Ensemble as a property on FetchSuccess* without a domain. Would avoid a new `DomainKind` but breaks the feature-driven requirement resolution -- no way to express "this feature needs spread data" in `DomainRequirements`.

**Rationale:** A first-class domain fits the existing architecture perfectly. Each domain polls independently, has its own failure backoff, and is gated by feature requirements. The gateway, PollWorker, and BudgetGuard already handle N domains -- adding one more is mechanical.

### D2: EnsemblePoint as a slim spread-only type

**Decision:** `EnsemblePoint` carries only `ValidFrom`, `TemperatureSpread`, `PrecipitationSpread`, `WindSpeedSpread`. Comparison model data lives in `FetchSuccess.Models` as `WeatherPoint` forecasts.

**Alternatives considered:**
- *Reusing WeatherPoint* for the ensemble primary forecast, adding all mean values plus spread. But the mean values are redundant with the deterministic forecast and would confuse consumers about which temperature to use.
- *No primary forecast, only Models*. But `FetchSuccess` requires a primary forecast, and the spread data is the natural primary output of the ensemble domain.

**Rationale:** Slim type communicates intent clearly: the ensemble domain's primary output is uncertainty metadata, not weather values. Comparison models are the secondary output in `Models`.

### D3: Spread fields removed from WeatherPoint (breaking)

**Decision:** `EnsembleStdDev`, `PrecipitationSpread`, `WindSpeedSpread` are removed from `WeatherPoint`. Since we're at 0.x, breaking persistence records is acceptable per project conventions.

**Rationale:** WeatherPoint now comes from the deterministic Forecast API which has no spread data. Keeping dead nullable fields would be misleading.

### D4: Alert confidence is opt-in per feature

**Decision:** 9 alerts that benefit from confidence declare `Ensemble` in their requirements. 5 alerts that don't benefit use `Medium` confidence without requiring Ensemble.

Split:
- **Require Ensemble:** Frost, Heat, Storm, Thunderstorm, Snow, Ice, HeavyRain, WindChill, TropicalNight
- **No Ensemble:** PressureDrop, Fog, Visibility, UV, Humidity

**Rationale:**
- PressureDrop: atmospheric pressure has very low ensemble spread; confidence adds no value
- Fog/Visibility: these are local phenomena below ensemble model resolution
- UV: depends on solar angle + clouds, not ensemble temperature spread
- Humidity: dew point spread not informative for the alert's purpose

### D5: ForecastClient uses best_match (no models parameter)

**Decision:** The `ForecastClient` sends no `models` parameter, using the API's automatic best_match blending.

**Alternatives considered:**
- *Specifying a model explicitly* (e.g. `ecmwf_ifs025`). Deterministic, but loses the multi-model high-resolution blending that best_match provides.

**Rationale:** best_match combines global backbone (ECMWF IFS, NOAA GFS, DWD ICON) with regional high-resolution models (AROME, ICON-D2, HRRR) based on location. This gives the highest possible resolution automatically without configuration.

### D6: EnsembleClient reuses WeatherResponse mapping

**Decision:** Both `ForecastClient` and `EnsembleClient` deserialize into `WeatherResponse` and use `SplitByModel`. The forecast client's plain keys route through the empty-key path; the ensemble client's suffixed keys route through the model-split path.

**Alternatives considered:**
- *Separate EnsembleResponse type*. More explicit, but the API responses have the same JSON structure.

**Rationale:** `SplitByModel` already handles both formats. The `EnsembleClient` additionally extracts spread fields into `EnsemblePoint` from the primary model's data before building the `Models` dictionary.

### D7: OpenMeteoEndpoints renames Weather → Ensemble, adds Forecast

**Decision:**
- `WeatherHost` → `EnsembleHost` (`https://ensemble-api.open-meteo.com/`)
- `WeatherPath` → `EnsemblePath` (`v1/ensemble`)
- New `ForecastHost` (`https://api.open-meteo.com/`)
- New `ForecastPath` (`v1/forecast`)

### D8: LocationForecast gains Ensemble property

**Decision:** `LocationForecast` gains a `DomainForecast<EnsemblePoint>? Ensemble` property alongside the existing `Weather`, `AirQuality`, etc. Alert rules that need confidence access it through their typed input (`WeatherEnsembleInput`).

## Risks / Trade-offs

- **[Two HTTP calls instead of one for Weather + Ensemble]** → The common case (alerts-only without ModelComparison) is cheaper (5 vs 8 calls). Full model comparison is more expensive but delivers better primary data.
- **[Breaking persistence records]** → Acceptable at 0.x. Old snapshots with spread fields on `WeatherPoint` will fail deserialization. Mitigation: actors recover from journal events; snapshots are rebuilt.
- **[Ensemble data arrives on a different cadence than Weather]** → By design -- domains poll independently. Alerts that require both will fire only after both have completed their first poll. This may delay the first alert by up to one ensemble poll cycle.
- **[best_match model may differ between locations]** → This is intentional -- each location gets the best available resolution. The model id is not exposed to features.
- **[Spread variables may be null for early forecast hours]** → `ConfidenceThresholds.LevelFor(null)` returns `Medium`, which is the safe default.

## Open Questions

None -- all decisions were resolved during exploration.
