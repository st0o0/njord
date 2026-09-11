# Architecture

njord is a .NET service built on Akka.NET and Akka.Streams. It polls the Open-Meteo API for weather forecasts, processes them through an enrichment pipeline, and exposes the results via gRPC for the ha-njord Home Assistant integration. An optional MQTT egress path is available for non-HA consumers.

## System Overview

<likec4-view view-id="index"></likec4-view>

njord runs as a single Docker container with no external database required (SQLite by default). It connects to the Open-Meteo API for forecast data and serves results to consumers via gRPC (primary) or MQTT (optional).

## Integration Paths

### Primary: ha-njord custom integration (gRPC)

The [ha-njord](https://github.com/st0o0/ha-njord) custom integration connects to njord via gRPC streaming on port 8081. It receives forecast, enrichment, and configuration updates in real time with no polling delay. The integration creates native Home Assistant entities across five platforms: `weather`, `sensor`, `binary_sensor`, `event`, and `button`.

### Alternative: MQTT

MQTT egress is disabled by default and intended for non-HA consumers such as Node-RED, custom dashboards, or other MQTT subscribers. When enabled, njord publishes forecast state and HA MQTT Discovery payloads to the configured broker. See the [MQTT reference](/mqtt-reference) for topic scheme and payload format.

## Three-Zone Design

The codebase is organized into three zones that only meet in the domain model:

<likec4-view view-id="internals"></likec4-view>

- **Ingest**: the Open-Meteo HTTP client, response parsing, and DTO mapping. Knows how to talk to the API but nothing about gRPC, MQTT, or Home Assistant.
- **Domain**: forecast models, enrichment features (consensus, alerts, trends, derived values, indices, history), and analysis logic. Pure domain, no I/O concerns.
- **Egress**: gRPC services, MQTT publishing, Home Assistant discovery payload construction, topic scheme, and availability management. Knows how to serve data but nothing about Open-Meteo.

Ingest and Egress never reference each other. All data flows through the domain model.

## gRPC Services

njord exposes four gRPC service groups on port 8081:

| Service | Methods | Purpose |
|---------|---------|---------|
| **WeatherService** | `GetCatalog`, `GetForecast`, `GetEnrichments`, `StreamForecasts`, `StreamEnrichments` | Forecast and enrichment data. `Stream*` methods push updates in real time. |
| **AdminService** | `GetConfig`, `StreamConfig`, `SetLocations`, `SetSettings`, `SetEnrichment`, `SetBudget` | Read and modify njord configuration at runtime. |
| **OpsService** | `GetStatus`, `GetTargets`, `TriggerPoll` | Server status, per-target poll state, and manual poll trigger. |
| **SensorService** | `Push`, `StreamPush` | Receive external sensor readings (indoor temperature, humidity). |

ha-njord uses `StreamForecasts`, `StreamEnrichments`, and `StreamConfig` for real-time updates, `GetCatalog` for initial discovery, and `TriggerPoll` for the poll button.

## Streaming Pipeline

The poll pipeline is an Akka.Streams graph that runs on each tick:

<likec4-view view-id="pipeline"></likec4-view>

1. **TickSource** fires at the configured `PollInterval` (default: 60 minutes)
2. **FanOut** expands the tick into one request per location/model pair
3. **Throttle** rate-limits requests to stay within the Open-Meteo API budget
4. **HTTP** executes the API calls via `OpenMeteoClient`
5. **Aggregate** groups responses by poll cycle with timeout and quorum
6. **Enrich** runs the enrichment pipeline (consensus, alerts, etc.)
7. **gRPC + MQTT** publishes results to connected gRPC streams and optionally to MQTT

Actors own connection lifecycle (gRPC stream management, MQTT connect/disconnect, Home Assistant birth-message handling) while streams handle the data flow. This separation keeps the pipeline testable and the lifecycle management isolated.

## External Sensor Input

The **SensorHub** actor accepts external sensor readings (e.g. indoor temperature, indoor humidity) via the gRPC `SensorService`. Readings are stored as latest values per sensor kind. During each poll cycle, enrichments (e.g. index scoring) pull the current snapshot from SensorHub. There is no reactive re-computation on sensor updates. If no sensor data is available, the enrichment falls back to the configured value or a hardcoded default.
