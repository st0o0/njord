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
- **Domain**: `PollManager`, one `PollWorker` per location (cluster sharding), `ForecastPipeline` with the `ProjectionEngine`, `BudgetTracker` and the domain alert handlers.
- **Consumers**: `GrpcConsumer` behind the gRPC API. They attach to the pipeline via SourceRef and know nothing about Open-Meteo.

Ingest and consumers never reference each other; all data flows through the domain model.

## Streaming pipeline

<likec4-view view-id="pipeline"></likec4-view>

1. **PollWorker** (one per location, started by `PollManager`) persists its learned update cycle and next poll time. When due, it asks the gateway for each enabled domain (`FetchDomain`); the three fetches run in parallel.
2. **Gateway** routes each request to Weather (Ensemble Mean), Air Quality or Flood and throttles to stay within the request budget.
3. **Fetch** parses the response into a `DomainForecast<T>` and offers a `DomainDataReceived` into the pipeline's `MergeHub`.
4. **ProjectionEngine** keeps a `LocationForecast` per location (current and previous state per domain), replaces the changed domain slot and invokes only the domain handlers triggered by that domain.
5. **Domain handlers** (`IDomainHandler`) read the `LocationForecast` and return `PipelineEvent`s such as `AlertRaised` (see below).
6. **BroadcastHub** emits typed `PipelineEvent`s (domain data received, alert raised) to `GrpcConsumer`.

Actors own lifecycle (polling, stream materialization, SourceRef handshakes); streams carry the data.

## Domain handlers

A domain handler declares the domains that trigger it and runs reactively when one of them is updated.

| Group | Domains | Rules |
|-------|---------|-------|
| Weather alerts | Weather | 14 rules on `DomainForecast<WeatherPoint>`; confidence from the ensemble standard deviation |
| Air quality alerts | Air Quality | Pollen, PM2.5 |
| Flood risk | Flood | FloodRisk: discharge against historical median, P75 and max |
| Flood rain (cross-domain) | Flood + Weather | FloodRain: at least 30 mm forecast rain in 24 h with rising discharge above the median |

Alerts are the only enrichment. Weather alerts reach gRPC consumers today. Air quality and flood alerts are computed by the pipeline as their own `PipelineEvent`s; their wire representation follows the weather alerts.

## gRPC services

| Service | Methods | Purpose |
|---------|---------|---------|
| **WeatherService** | `GetCatalog`, `GetDomainData`, `StreamDomainData`, `GetEnrichments`, `StreamEnrichments` | Domain forecast data and alerts; `Stream*` push updates in real time |
| **OpsService** | `GetStatus`, `GetTargets`, `TriggerPoll` | Version, uptime, budget, per-location poll state, manual poll |

## Integration paths

- **Home Assistant (primary):** ha-njord connects to port 8081 and streams updates. See [Home Assistant](/home-assistant/).

## Configuration

Configuration is static: `appsettings.json` plus environment variables, read at startup. There is no runtime configuration API; change a setting and restart the container.
