# Architecture

njord has two parts: a .NET service that polls Open-Meteo and enriches the data with alerts, and a [Home Assistant custom integration](https://github.com/st0o0/ha-njord) that presents the results as native entities. The service exposes data over gRPC (primary) .

## System overview

<likec4-view view-id="index"></likec4-view>

njord runs as one Docker container without an external database (SQLite by default). It fetches forecasts from Open-Meteo and serves them to consumers over gRPC on port 8081.

## Domains

Each poll cycle fetches the domains that the enabled [features](/configuration/#features-and-domains) need. The defaults poll Forecast, Ensemble and Air Quality:

| Domain | Open-Meteo API | Content |
|--------|----------------|---------|
| **Forecast** | `api.open-meteo.com/v1/forecast` | High-resolution deterministic forecast using automatic best_match model blending (1-9 km). All weather variables including precipitation probability and UV index. |
| **Ensemble** | Ensemble Mean (`ensemble-api.open-meteo.com/v1/ensemble`) | Temperature, precipitation and wind spread (standard deviation across 51 ensemble members) for alert confidence. Comparison model forecasts when ModelComparison is enabled. |
| **Air Quality** | Air Quality | PM2.5, PM10, ozone, nitrogen dioxide, pollen (birch, grass, alder, mugwort, ragweed, olive), European and US AQI sub-indices |
| **Flood** | Flood | River discharge with mean, median, min, max, P25 and P75 |
| **Marine** | Marine | Wave height, period, direction (wind waves and swell), sea surface temperature, sea level, ocean currents |
| **Seasonal** | Seasonal | Monthly temperature and precipitation anomalies for the next 3-6 months |
| **Satellite** | Satellite Archive | Observed solar irradiance (GHI, direct, diffuse, DNI, tilted) for blending with the forecast |

Forecast and Ensemble are separate domains that poll independently. The Forecast domain delivers the primary forecast values; the Ensemble domain delivers uncertainty data and optional model comparison. Features that need confidence (9 of the 20 weather alerts) declare Ensemble in their requirements; features that don't need it use a fixed Medium confidence.

## Three-zone design (7 domains)

<likec4-view view-id="internals"></likec4-view>

- **Ingest**: `OpenMeteoGateway` routes each domain request to its client (`ForecastClient`, `EnsembleClient`, `AirQualityClient`, `FloodClient`, `MarineClient`, `SeasonalClient`, `SatelliteRadiationClient`) and rate-limits against the budget. Clients build requests and parse responses. They know nothing about gRPC or Home Assistant.
- **Domain**: `PollManager`, one `PollWorker` per location (cluster sharding), `ForecastPipeline` with the `FeatureEngine`, `BudgetTracker` and the 58 feature rules (`Njord.Features`).
- **Consumers**: `GrpcConsumer` behind the gRPC API. They attach to the pipeline via SourceRef and know nothing about Open-Meteo.

Ingest and consumers never reference each other; all data flows through the domain model.

## Streaming pipeline

<likec4-view view-id="pipeline"></likec4-view>

1. **PollWorker** (one per location, started by `PollManager`) persists its learned update cycle and next poll time per domain. When due, it asks the gateway for each enabled domain (`FetchDomain`); fetches run in parallel.
2. **Gateway** routes each request to the matching domain client (Forecast, Ensemble, Air Quality, Flood, Marine, Seasonal, Satellite) and throttles to stay within the request budget.
3. **Fetch** parses the response into a `DomainForecast<T>` and offers a `DomainDataReceived` into the pipeline's `MergeHub`.
4. **FeatureEngine** keeps a ring of recent `LocationForecast`s per location, replaces the changed domain slot and runs only the enabled feature rules that read that domain and whose required fields are present.
5. **Feature rules** (`IFeatureRule`, usually `FeatureRule<TInput>`) project the `LocationForecast` into a typed input and return typed `FeatureResult` events such as alerts, scores or conditions (see below).
6. **BroadcastHub** emits typed `PipelineEvent`s (domain data received, alert raised) to `GrpcConsumer`.

Actors own lifecycle (polling, stream materialization, SourceRef handshakes); streams carry the data.

## Feature rules

A feature rule declares its required domains and fields in a `FeatureDescriptor` and runs reactively when one of them is updated. It is switched with `Njord:Features:<Category>:<Name>:Enabled`.

| Group | Domains | Rules |
|-------|---------|-------|
| Weather alerts | Forecast (+ Ensemble for confidence) | 20 alerts: frost, heat, storm, heavy rain, UV, fog, snow, pressure drop, thunderstorm, ice, wind chill, visibility, tropical night, humidity, rain probability, wet-bulb danger, clear-sky UV, shower, snow depth, fire risk |
| Air quality alerts | Air Quality (+ Forecast for SmogRisk) | Pollen, PM2.5, Dust, SmogRisk, PollenBlast |
| Flood alerts | Flood (+ Forecast for FloodRain) | FloodRisk, FloodRain |
| Marine alerts | Marine (+ Forecast for CoastalFlood) | Swimming, CoastalFlood |
| Lifestyle scores | Forecast (+ Air Quality for some) | BBQ, Laundry, Outdoor, Commute, Exercise |
| Conditions | Various | SoilMoisture, Evapotranspiration, Gardening, Ventilation, NightVentilation, SurfConditions, OceanCurrents, SnowDepthWatch, PollenTrend, ElevatedWind |
| Trends and forecasts | Various | ConfidenceTrend, FrontApproaching, ModelComparison, SeasonalOutlook, GrowingDegree, SolarYield, SatelliteRadiation |

58 feature rules across 8 categories. Every feature result reaches gRPC consumers as an `EnrichmentEvent` whose `feature` oneof case identifies the result type.

## gRPC services

| Service | Methods | Purpose |
|---------|---------|---------|
| **WeatherService** | `GetCatalog`, `GetDomainData`, `StreamDomainData`, `GetEnrichments`, `StreamEnrichments` | Domain forecast data and alerts; `Stream*` push updates in real time |
| **OpsService** | `GetStatus`, `GetTargets`, `TriggerPoll` | Version, uptime, budget, per-location poll state, manual poll |

## Integration paths

- **Home Assistant (primary):** ha-njord connects to port 8081 and streams updates. See [Home Assistant](/home-assistant/).

## Configuration

Configuration is static: `appsettings.json` plus environment variables, read at startup. There is no runtime configuration API; change a setting and restart the container.
