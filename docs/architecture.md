# Architecture

njord is a weather intelligence system in two parts: a .NET service that polls the Open-Meteo API and enriches forecasts, and a [Home Assistant custom integration](https://github.com/st0o0/ha-njord) that presents the results as native HA entities. The service exposes data via gRPC (primary, used by the integration) and MQTT (optional, for non-HA consumers).

## System Overview

<likec4-view view-id="index"></likec4-view>

njord runs as a single Docker container with no external database required (SQLite by default). It connects to the Open-Meteo API for forecast data and serves results to consumers via gRPC (primary) or MQTT (optional).

## Integration Paths

### Primary: Home Assistant custom integration (gRPC)

The [ha-njord](https://github.com/st0o0/ha-njord) custom integration connects to njord via gRPC streaming on port 8081. It receives forecast, enrichment, and configuration updates in real time with no polling delay. The integration creates native Home Assistant entities across five platforms: `weather`, `sensor`, `binary_sensor`, `event`, and `button`. See the [Home Assistant](/home-assistant/) section for details.

### Alternative: MQTT

MQTT egress is disabled by default and intended for non-HA consumers such as Node-RED, custom dashboards, or other MQTT subscribers. When enabled, njord publishes retained forecast and analysis state to the configured broker; it does not publish MQTT Discovery payloads. See the [MQTT reference](/mqtt-reference) for topic scheme and payload format.

## Three-Zone Design

The codebase is organized into three zones that only meet in the domain model:

<likec4-view view-id="internals"></likec4-view>

- **Ingest**: the `OpenMeteoGateway` fetch stream and the `OpenMeteoClient` (request building, response parsing, DTO mapping). Knows how to talk to the API but nothing about gRPC, MQTT, or Home Assistant.
- **Domain**: the `PollManager` with one persistent `PollWorker` per location/model target (cluster sharding), the `ForecastPipeline` analysis stream, the `BudgetTracker`, the `SensorHub`, and the analysis libraries (consensus, alerts, derived values, trends, indices).
- **Consumers**: output adapters that attach to the pipeline via SourceRef: the `ForecastReadModel` behind the gRPC API and the optional `MqttStatePublisher` (state topics and availability). They know how to serve data but nothing about Open-Meteo.

Ingest and consumers never reference each other. All data flows through the domain model.

## gRPC Services

njord exposes four gRPC service groups on port 8081:

| Service | Methods | Purpose |
|---------|---------|---------|
| **WeatherService** | `GetCatalog`, `GetForecast`, `GetEnrichments`, `StreamForecasts`, `StreamEnrichments` | Forecast and enrichment data. `Stream*` methods push updates in real time. |
| **AdminService** | `GetConfig`, `StreamConfig`, `SetLocations`, `SetSettings`, `SetEnrichment`, `SetBudget` | Read and modify njord configuration at runtime. |
| **OpsService** | `GetStatus`, `GetTargets`, `TriggerPoll` | Server status, per-target poll state, and manual poll trigger. |
| **SensorService** | `Push`, `StreamPush` | Receive external sensor readings (indoor temperature, humidity). |

The HA integration uses `StreamForecasts`, `StreamEnrichments`, and `StreamConfig` for real-time updates, `GetCatalog` for initial discovery, and `TriggerPoll` for the poll button.

## Streaming Pipeline

The data flow spans two actors, `OpenMeteoGateway` (fetch) and `ForecastPipeline` (analysis), connected by a `SourceRef<Forecast>`:

<likec4-view view-id="pipeline"></likec4-view>

1. **PollWorker** (one per location/model target, started by the `PollManager`) decides when its target is due and asks the gateway with `FetchForecast`; at most one request per target is in flight
2. **GatewayThrottleStage** rate-limits the queued targets with a token bucket to stay within the Open-Meteo API budget
3. **Fetch** runs up to two `OpenMeteoClient` calls in parallel and answers each request to the poll worker that asked
4. **Forecast hub** broadcasts every successful `Forecast` to the `ForecastPipeline`
5. **Analyze** combines each forecast with the latest sensor readings and computes consensus, then every enabled analysis feature (alerts, derived values, trends, indices)
6. **PipelineEvent hub** broadcasts typed events to the consumers: the `ForecastReadModel` (gRPC API) and, when enabled, the `MqttStatePublisher`

Actors own lifecycle (stream materialization, SourceRef handshakes, MQTT connect/reconnect and availability) while streams handle the data flow. This separation keeps the pipeline testable and the lifecycle management isolated.

## External Sensor Input

The **SensorHub** actor accepts external sensor readings (e.g. indoor temperature, indoor humidity) via the gRPC `SensorService`. Readings are stored as latest values per sensor kind. During each poll cycle, enrichments (e.g. index scoring) pull the current sensor readings from SensorHub. There is no reactive re-computation on sensor updates. If no sensor data is available, the enrichment falls back to the configured value or a hardcoded default.
