# Architecture

njord has two parts: a .NET service that polls Open-Meteo and enriches the data with alerts, and a [Home Assistant custom integration](https://github.com/st0o0/ha-njord) that presents the results as native entities. The service exposes data over gRPC (primary) .

## System overview

<likec4-view view-id="index"></likec4-view>

njord runs as one Docker container without an external database (SQLite by default). It fetches forecasts from Open-Meteo and serves them to consumers over gRPC on port 8081.

## Domains

Each poll cycle fetches the domains that the enabled [features](/configuration/#features-and-domains) need (Weather and Air Quality by default):

| Domain | Open-Meteo API | Content |
|--------|----------------|---------|
| **Weather** | Ensemble Mean | Hourly and daily weather values plus `ensemble_std_dev`; solar radiation variables when a feature needs them |
| **Air Quality** | Air Quality | PM2.5, PM10, ozone, pollen (birch, grass, alder, mugwort) |
| **Flood** | Flood | River discharge with mean, median, min, max, P25 and P75 |

The Ensemble Mean API replaces polling individual models: one call per location returns a statistically robust forecast, with the ensemble spread as a confidence signal.

## Three-zone design

<likec4-view view-id="internals"></likec4-view>

- **Ingest**: `OpenMeteoGateway` routes each domain request to its endpoint and rate-limits against the budget. `OpenMeteoClient` builds requests and parses responses. It knows nothing about gRPC or Home Assistant.
- **Domain**: `PollManager`, one `PollWorker` per location (cluster sharding), `ForecastPipeline` with the `FeatureEngine`, `BudgetTracker` and the feature rules (`Njord.Features`).
- **Consumers**: `GrpcConsumer` behind the gRPC API. They attach to the pipeline via SourceRef and know nothing about Open-Meteo.

Ingest and consumers never reference each other; all data flows through the domain model.

## Streaming pipeline

<likec4-view view-id="pipeline"></likec4-view>

1. **PollWorker** (one per location, started by `PollManager`) persists its learned update cycle and next poll time. When due, it asks the gateway for each enabled domain (`FetchDomain`); the three fetches run in parallel.
2. **Gateway** routes each request to Weather (Ensemble Mean), Air Quality or Flood and throttles to stay within the request budget.
3. **Fetch** parses the response into a `DomainForecast<T>` and offers a `DomainDataReceived` into the pipeline's `MergeHub`.
4. **FeatureEngine** keeps a ring of recent `LocationForecast`s per location, replaces the changed domain slot and runs only the enabled feature rules that read that domain and whose required fields are present.
5. **Feature rules** (`IFeatureRule`, usually `FeatureRule<TInput>`) project the `LocationForecast` into a typed input and return typed `FeatureResult` events such as alerts, scores or conditions (see below).
6. **BroadcastHub** emits typed `PipelineEvent`s (domain data received, alert raised) to `GrpcConsumer`.

Actors own lifecycle (polling, stream materialization, SourceRef handshakes); streams carry the data.

## Feature rules

A feature rule declares its required domains and fields in a `FeatureDescriptor` and runs reactively when one of them is updated. It is switched with `Njord:Features:<Category>:<Name>:Enabled`.

| Group | Domains | Rules |
|-------|---------|-------|
| Weather alerts | Weather | 14 rules on `DomainForecast<WeatherPoint>`; confidence from the ensemble standard deviation |
| Air quality alerts | Air Quality | Pollen, PM2.5 |
| Flood risk | Flood | FloodRisk: discharge against historical median, P75 and max |
| Flood rain (cross-domain) | Flood + Weather | FloodRain: at least 30 mm forecast rain in 24 h with rising discharge above the median |

Besides alerts there are scores, conditions, trends and forecasts. Every feature result reaches gRPC consumers as an `EnrichmentEvent` whose `feature` oneof case identifies the result type.

## gRPC services

| Service | Methods | Purpose |
|---------|---------|---------|
| **WeatherService** | `GetCatalog`, `GetDomainData`, `StreamDomainData`, `GetEnrichments`, `StreamEnrichments` | Domain forecast data and alerts; `Stream*` push updates in real time |
| **OpsService** | `GetStatus`, `GetTargets`, `TriggerPoll` | Version, uptime, budget, per-location poll state, manual poll |

## Integration paths

- **Home Assistant (primary):** ha-njord connects to port 8081 and streams updates. See [Home Assistant](/home-assistant/).

## Configuration

Configuration is static: `appsettings.json` plus environment variables, read at startup. There is no runtime configuration API; change a setting and restart the container.
