## Context

`WeatherClient` sends `models=<WeatherOptions.Model>` to `v1/ensemble`. One fetch yields one `DomainForecast<WeatherPoint>` per location; `LocationForecast.Weather` holds it, every `IDomainHandler` reads it, gRPC serves it as `DomainDataUpdate(location, DOMAIN_WEATHER)`. `PollWorker` persists only the response hash per domain.

## Decisions

### 1. One batched request, N forecasts out
The Open-Meteo multi-model response returns the same hourly variables once per model with the model id appended (for example `temperature_2m_icon_seamless_ensemble_mean`). `WeatherResponse` gains a per-model hourly map (custom converter registered in `OpenMeteoJsonContext`, source-generated JSON stays); `WeatherResponseMappings.ToDomainForecasts(response, models, modelRun)` returns a dictionary of model id to `DomainForecast<WeatherPoint>`. A single-model request keeps unsuffixed keys, so the converter handles both shapes. A model with no values is simply absent (partial success).

Rejected: one request per model. Same data, N times the requests and rate-limit pressure; it also breaks the "one FetchDomain gets exactly one outcome" rule.

### 2. Primary model plus side map, not a new key everywhere
`LocationForecast.Weather` stays the primary model; `LocationForecast.ModelWeather` (immutable dictionary, model id to forecast) holds the others. Handlers, `HasFields`, alert rules, MQTT and `Previous` trend logic need no change. Rejected: keying `Weather` by model (touches every handler) and "virtual locations" (multiplies PollWorkers, timers and persistence entities, although the models share one fetch and must share one worker).

`DomainDataReceived` gets `ModelId` (`string?`, null = primary). `ProjectionEngine` merges by model id; enrichments run only when the primary model arrives or changes. A change in a secondary model emits a data event but no enrichment run.

### 3. Fetch contract unchanged
`FetchDomain(location, Weather)` still gets exactly one `FetchDomainCompleted` (hash over the whole response), `Failed` or `Rejected`. Data reaches the pipeline through the existing SinkRef branch, one `DomainDataReceived` per model. The learned update cycle stays per domain; since the hash changes when any model updates, polling follows the fastest model. No persistence record change.

### 4. Model catalog
`WeatherModelCatalog` is a static table (id, display name, provider, region, resolution km, forecast hours) of the supported ensemble-mean ids. It starts with ECMWF IFS 0.25, ICON seamless, GFS seamless, GEM global and UKMO global, and is extended for MeteoSwiss, MET Norway, KMA, JMA and BOM after checking each id against the API. Unknown ids fail validation rather than producing an HTTP 400 at runtime. The same data is mirrored in `docs/data/` for the builder; a spec compares both.

### 5. Feature gating
`FeatureKind.ModelComparison` (category Weather, default off, required domain Weather, marginal cost = (modelCount - 1) x weather weight per model x polls). Effective models = `Models` when ModelComparison is enabled, else `Models[0]`. A config with several models but the feature off logs one startup warning.

### 6. gRPC
Additive proto fields: `GetDomainDataRequest.model`, `DomainDataUpdate.model`, `ModelInfo.enabled`. Empty request model = primary; unknown or disabled model = `NOT_FOUND`. Only weather updates carry a model. Older ha-njord clients ignore the fields and see the primary model, so the change is wire compatible.

### 7. ha-njord (noted, separate repo)
Entities: `weather.njord_<location>` for the primary model, `weather.njord_<model_slug>_<location>` for the rest, built from `GetCatalog` plus streamed `model` values. Extra attributes: model name, resolution_km, forecast length. The default entity gets a friendly name that includes the model.

## Risks

- Call weighting of multi-model requests is unverified (see proposal); mitigated by the conservative multiplier and task 1.1.
- Memory and stream volume scale with the model count; bounded by the drop-oldest buffers and the catalog size.
- Models with shorter horizons return null tails; the existing "trim to last valued point" logic applies per model.
