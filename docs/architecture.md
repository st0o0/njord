# Architecture

njord is a weather intelligence proxy in two parts: a .NET service that polls the Open-Meteo API (Ensemble Mean and domain-specific endpoints) and enriches the data with alerts, and a [Home Assistant custom integration](https://github.com/st0o0/ha-njord) that presents the results as native HA entities. The service exposes data via gRPC (primary, used by the integration) and MQTT (optional, for non-HA consumers).

## System Overview

<likec4-view view-id="index"></likec4-view>

njord runs as a single Docker container with no external database required (SQLite by default). It connects to the Open-Meteo API for forecast data and serves results to consumers via gRPC (primary) or MQTT (optional).

## Integration Paths

### Primary: Home Assistant custom integration (gRPC)

The [ha-njord](https://github.com/st0o0/ha-njord) custom integration connects to njord via gRPC streaming on port 8081. It receives domain data and enrichment updates in real time with no polling delay. The integration creates native Home Assistant entities across five platforms: `weather`, `sensor`, `binary_sensor`, `event`, and `button`. See the [Home Assistant](/home-assistant/) section for details.

### Alternative: MQTT

MQTT egress is disabled by default and intended for non-HA consumers such as Node-RED, custom dashboards, or other MQTT subscribers. When enabled, njord publishes retained domain data and alert state to the configured broker; it does not publish MQTT Discovery payloads. See the [MQTT reference](/mqtt-reference) for topic scheme and payload format.

## Three-Zone Design

The codebase is organized into three zones that only meet in the domain model:

<likec4-view view-id="internals"></likec4-view>

- **Ingest**: the `OpenMeteoGateway` with multi-domain routing (Ensemble, Air Quality, Flood, Solar, Marine endpoints) and the `OpenMeteoClient` (request building, response parsing, DTO mapping). Knows how to talk to the API but nothing about gRPC, MQTT, or Home Assistant.
- **Domain**: the `PollManager` with one persistent `PollWorker` per location (cluster sharding), the `ForecastPipeline` with `ProjectionEngine` and domain-specific enrichment groups, the `BudgetTracker`, and the alert library.
- **Consumers**: output adapters that attach to the pipeline via SourceRef: the `GrpcConsumer` behind the gRPC API and the optional `MqttStatePublisher` (domain-based state topics and availability). They know how to serve data but nothing about Open-Meteo.

Ingest and consumers never reference each other. All data flows through the domain model.

## gRPC Services

njord exposes two gRPC service groups on port 8081:

| Service | Methods | Purpose |
|---------|---------|---------|
| **WeatherService** | `GetCatalog`, `GetDomainData`, `GetEnrichments`, `StreamDomainData`, `StreamEnrichments` | Domain forecast data and enrichment (alerts). `Stream*` methods push updates in real time. |
| **OpsService** | `GetStatus`, `GetTargets`, `TriggerPoll` | Server status, per-location poll state, and manual poll trigger. |

Configuration is static (appsettings.json + environment variables). Changes require a container restart.

## Streaming Pipeline

The data flow spans two actors, `OpenMeteoGateway` (fetch) and `ForecastPipeline` (analysis), connected by a `SinkRef<DomainDataReceived>`:

<likec4-view view-id="pipeline"></likec4-view>

1. **PollWorker** (one per location, started by the `PollManager`) decides when its location is due and asks the gateway with `FetchDomain` for each enabled domain; domain fetches run in parallel
2. **Gateway** routes each `FetchDomain` to the correct Open-Meteo endpoint (Ensemble Mean for weather, /air-quality, /flood, etc.) and rate-limits requests to stay within the API budget
3. **Fetch** runs the `OpenMeteoClient` call, parses the response into a `DomainForecast<T>`, and offers `DomainDataReceived` into the pipeline
4. **ProjectionEngine** maintains a `LocationForecast` read-model per location (current + previous state), updates the changed domain slot, and evaluates only the `ProjectionGroup`s affected by that domain
5. Each **ProjectionGroup** projects the LocationForecast into a typed enrichment input and runs its enrichments (e.g., the `WeatherAlertGroup` runs 14 alert rules on `DomainForecast<WeatherPoint>` with confidence from Ensemble StdDev)
6. **BroadcastHub** sends typed `PipelineEvent`s (domain data received, alerts raised) to the consumers: `GrpcConsumer` and, when enabled, `MqttStatePublisher`

Actors own lifecycle (stream materialization, SourceRef handshakes, MQTT connect/reconnect and availability) while streams handle the data flow. This separation keeps the pipeline testable and the lifecycle management isolated.
