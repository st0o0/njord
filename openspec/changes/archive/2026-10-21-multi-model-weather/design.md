## Context

The thick `WeatherClient` (`IDomainClient`, from `rewrite-gateway`) sends `models=<WeatherOptions.Model>` to `v1/ensemble` and returns one `FetchSuccess`. One fetch yields one `DomainForecast<WeatherPoint>` per location; `LocationForecast.Weather` holds it, every `FeatureRule` projects from it, gRPC serves it as `DomainDataUpdate(location, DOMAIN_WEATHER)`. `PollWorker` persists only the response hash per domain.

## Decisions

### 1. One batched request, N forecasts out
The Open-Meteo multi-model response returns the same hourly variables once per model with the model id appended (for example `temperature_2m_icon_seamless_ensemble_mean`). `WeatherResponse` gains a per-model hourly map (custom converter registered in `OpenMeteoJsonContext`, source-generated JSON stays); `WeatherResponseMappings.ToDomainForecasts(response, models, modelRun)` returns a dictionary of model id to `DomainForecast<WeatherPoint>`. A single-model request keeps unsuffixed keys, so the converter handles both shapes. A model with no values is simply absent (partial success).

Rejected: one request per model. Same data, N times the requests and rate-limit pressure; it also breaks the "one FetchDomain gets exactly one outcome" rule.

### 2. Primary model plus side map, not a new key everywhere
`LocationForecast.Weather` stays the primary model; `LocationForecast.ModelWeather` (immutable dictionary, model id to forecast) holds the others. Feature rules, `HasFields` and the trend ring need no change. Rejected: keying `Weather` by model (touches every feature rule) and "virtual locations" (multiplies PollWorkers, timers and persistence entities, although the models share one fetch and must share one worker).

`DomainDataReceived` gets `ModelId` (`string?`, null = primary). `FeatureEngine` merges by model id; feature rules run only when the primary model arrives or changes. A change in a secondary model emits a data event but no enrichment run.

### 3. Fetch contract unchanged
`FetchDomain(location, Weather)` still gets exactly one `FetchDomainCompleted` (hash over the whole response), `Failed` or `Rejected`. Data reaches the pipeline through the existing SinkRef branch, one `DomainDataReceived` per model. The learned update cycle stays per domain; since the hash changes when any model updates, polling follows the fastest model. No persistence record change.

### 4. Model catalog
`WeatherModelCatalog` is a static table (id, display name, provider, region, resolution km, forecast hours) of the supported ensemble-mean ids. It starts with the three ids verified in task 1.1 (`ecmwf_ifs025_ensemble_mean`, `ecmwf_aifs025_ensemble_mean`, `ukmo_global_ensemble_mean_20km`) and is extended only after checking each further id against the API. Unknown ids fail validation rather than producing an HTTP 400 at runtime. The same data is mirrored in `docs/data/` for the builder; a spec compares both.

### 5. Feature gating
`ModelComparison` feature (`OutputKind.Forecast`, category Weather, default off, required domain Weather, marginal cost = (modelCount - 1) x weather weight per model x polls). Effective models = `Models` when ModelComparison is enabled, else `Models[0]`. A config with several models but the feature off logs one startup warning.

### 6. gRPC
Additive proto fields: `GetDomainDataRequest.model`, `DomainDataUpdate.model`, `ModelInfo.enabled`. Empty request model = primary; unknown or disabled model = `NOT_FOUND`. Only weather updates carry a model. Older ha-njord clients ignore the fields and see the primary model, so the change is wire compatible.

### 7. ha-njord (noted, separate repo)
Entities: `weather.njord_<location>` for the primary model, `weather.njord_<model_slug>_<location>` for the rest, built from `GetCatalog` plus streamed `model` values. Extra attributes: model name, resolution_km, forecast length. The default entity gets a friendly name that includes the model.

Contract for the ha-njord follow-up (njord side is done):
- `GetCatalog` lists every known model with `ModelInfo.enabled`; create entities only for `enabled` models. The first enabled entry is the primary model (the server sends the primary id in `DomainDataUpdate.model` for weather updates).
- Entity ids: `weather.njord_<location>` stays the primary model (unchanged for existing installs); secondary models get `weather.njord_<model_slug>_<location>` where the slug is the model id (`ecmwf_aifs025_ensemble_mean`).
- Attributes per model entity: `model` (id), `model_name` (display name), `provider`, `region`, `resolution_km`, `forecast_hours` (`max_forecast_hours`).
- Requests: `GetDomainData` without `model` returns the primary; an unknown or disabled model returns `NOT_FOUND`. `StreamDomainData` emits one weather update per effective model, each with `model` set; non-weather updates carry no model.
- `EnrichmentEvent.model_comparison` (oneof case 91, `ModelComparison`) carries the per-day temperature and precipitation of each model; only present when `Weather:ModelComparison` is enabled.
- Older ha-njord versions ignore the new fields and see the primary model only.

## Risks

- Call weighting of multi-model requests is unverified (see proposal); mitigated by the conservative multiplier and task 1.1.
- Memory and stream volume scale with the model count; bounded by the drop-oldest buffers and the catalog size.
- Models with shorter horizons return null tails; the existing "trim to last valued point" logic applies per model.

## Task 1.1 findings (live probe, 2026-10-21)

- With two or more models, every hourly key carries the model suffix (`temperature_2m_ecmwf_ifs025_ensemble_mean`); with one model the keys are unsuffixed. The converter handles both shapes (decision 1 holds).
- A raw ensemble id (`icon_seamless_eps`) also returns every member (`temperature_2m_member01_icon_seamless_eps`, ...). The mapping ignores `memberNN` keys, and the catalog only admits ensemble-mean ids so responses stay small.
- Valid ensemble-mean ids today: `ecmwf_ifs025_ensemble_mean`, `ecmwf_aifs025_ensemble_mean`, `ukmo_global_ensemble_mean_20km`. `bom_access_global_ensemble_mean` is accepted but returned only nulls for Berlin; it stays out until it delivers data. ICON, GFS and GEM have no `_ensemble_mean` id (HTTP 400). Decision 4's starting list shrinks to these three ids; further ids are added only after the same check.
- The response does not report its call weight. The conservative `ceil(variables / 10) x modelCount` stays.

## FetchSuccess shape (decided 2026-10-21)

`FetchSuccess(IDomainForecast Forecast)` stays the primary forecast and gains `ImmutableDictionary<string, IDomainForecast> Models` (default empty) for the secondary models. The five single-model clients do not change. The gateway offers `Forecast` as `DomainDataReceived` with `ModelId = null` and one more per `Models` entry. Rejected: `FetchSuccess(IReadOnlyList<ModelForecast>)` (touches every client for one domain's need) and a separate `FetchModelsSuccess` (a second success arm in the gateway for the same outcome).
