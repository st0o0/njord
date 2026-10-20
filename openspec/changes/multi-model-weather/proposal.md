## Why

Model comparison is something no other Home Assistant weather integration offers: users can see how ECMWF, ICON, GFS and others predict their local weather and pick the one that fits their region. The Open-Meteo ensemble API already accepts several models in one request; njord only ever sends the single `WeatherOptions.Model`.

## What Changes

- Add `WeatherOptions.Models` (`Njord:Weather:Models`, list of model ids, default `["ecmwf_ifs025_ensemble_mean"]`) in `src/Njord.Core/Configuration/WeatherOptions.cs`. The first entry is the **primary model**. **BREAKING**: the single `WeatherOptions.Model` (`Njord:Weather:Model`) is removed; while 0.x a startup validation error names the old key. `WeatherOptionsValidator` rejects empty lists, duplicates and ids missing from a new known-model catalog.
- Add a model catalog in `Njord.Core` (`Domain/WeatherModelCatalog.cs`): id, display name, provider, region, resolution km, forecast length. It feeds validation, the gRPC `GetCatalog` `ModelInfo` entries and the Config Builder.
- `WeatherClient` (`src/Njord.OpenMeteo/WeatherClient.cs`) sends all enabled models in one `models=a,b,c` request. `WeatherResponse` keeps one hourly block per model (Open-Meteo suffixes every variable with the model id in multi-model responses); `WeatherResponseMappings` yields one `DomainForecast<WeatherPoint>` per model.
- `DomainDataReceived` and the pipeline carry a model id. `LocationForecast.Weather` stays the primary model's forecast (all handlers and alert rules keep working unchanged); a new `LocationForecast.ModelWeather` holds the forecasts of the additional models keyed by model id.
- The `ModelComparison` feature (`OutputKind.Forecast`, category Weather, off by default) gates fetching more than the primary model. Without it only the primary model is requested even if `Models` lists more.
- gRPC (`protos/njord/v2/weather.proto`): `model` field on `GetDomainDataRequest` (empty = primary) and on `DomainDataUpdate`; `StreamDomainData` emits one update per model. `GetCatalog` marks which models are enabled.
- Config Builder lists models with resolution and forecast length, toggles them, and shows the budget impact per model.
- ha-njord co-design (separate repo `D:\Git\ha_njord`, noted only): one weather entity per model per location.

## Capabilities

### New Capabilities
- `multi-model-weather`: model selection config and validation, single batched request, per-model forecasts through gateway and pipeline, primary-model semantics for enrichments, budget accounting.

### Modified Capabilities
- `grpc-v2-weather-service`: per-model domain data (request and update `model` field, catalog enabled flag).
- `config-builder`: model toggles with resolution, forecast length and per-model budget impact.

## Non-goals

- Model blending or averaging; each model's raw data is shown as is.
- Automatic model selection or accuracy scoring; the user picks the models.
- Running alert rules, lifestyle handlers or MQTT per model; enrichments consume the primary model only.
- Non-weather domains: AirQuality, Flood and Marine stay single-source.
- Implementing the ha-njord entities; this change only specifies the contract and notes (ha-njord gets its own change).
- Changing the variable set or the poll timers.

## API-budget impact

Number of HTTP requests: **0 extra**. All models go into one `models=` request per location and poll cycle, so requests/month stay identical (1 location, 60 min = ~720 polls/month).

Call weight is the open question. Open-Meteo weights a request by variables, days and locations, and multiple models in one call are expected to be counted per model. This proposal therefore assumes the **conservative** rule until measured: weather weight = `ceil(variables / 10) x modelCount`. Standard 31 variables = 4 per model, so at 1 location and 720 polls: 1 model = 2,880 calls/month, 3 models = 8,640, 5 models = 14,400 (4.8 % of 300k). Ten locations with 3 models = 86,400 (28.8 %), within the 80 % startup gate. `BudgetCalculator` multiplies the weather weight by the number of effective models, and the startup check, the health budget and the Config Builder all use it. Task 1.1 verifies the real weighting against the live API before the formula is finalized; if multi-model turns out cheaper, only the multiplier changes.

Responses grow with the model count (N x variables of ensemble means); the free tier has no byte limit and ensemble means are small compared to full members.

## Impact

- `src/Njord.Core`: `Configuration/WeatherOptions.cs`, `WeatherOptionsValidator.cs`, `Configuration/Polling/BudgetCalculator.cs`, `Domain/LocationForecast.cs`, `DomainDataReceived`, new `Domain/WeatherModelCatalog.cs`, the `ModelComparison` feature descriptor.
- `src/Njord.OpenMeteo`: `WeatherClient.cs`, `WeatherResponse.cs`, `Mapping/WeatherResponseMappings.cs`, the gateway only if `FetchSuccess` must carry several forecasts (design, open point).
- `src/Njord.Messages/Forecast`: model id on `QueryDomainData` / `DomainDataNotAvailable`.
- `src/Njord.Pipeline`: `ForecastPipeline` / `ProjectionEngine` keep per-model state in the closure.
- `src/Njord.Grpc` and `protos/njord/v2/weather.proto`: new fields (additive, wire compatible).
- `docs/.vitepress/theme/builder/*`, `docs/data/*`, `docker-compose.example.yml`, `src/Njord/appsettings.Example.json`, AGENTS.md.
- Sequencing: build after `rewrite-gateway`. The thick `WeatherClient` (`IDomainClient`) builds the `models=` request, maps the response and returns one forecast per model; the thin gateway only forwards them. `FeatureKind` no longer exists, so `ModelComparison` is declared like the other features (`FeatureDescriptor` with `OutputKind.Forecast`, toggle, `docs/data/features.json`).
