# Architecture

njord is a .NET service built on Akka.NET and Akka.Streams. It polls the Open-Meteo API for weather forecasts, processes them through an enrichment pipeline, and publishes the results to Home Assistant via MQTT Discovery.

## System Overview

<likec4-view view-id="index"></likec4-view>

njord sits between the Open-Meteo API and Home Assistant, connected through an MQTT broker (typically Mosquitto). It runs as a single Docker container with no external database required (SQLite by default).

## Three-Zone Design

The codebase is organized into three zones that only meet in the domain model:

<likec4-view view-id="internals"></likec4-view>

- **Ingest** — the Open-Meteo HTTP client, response parsing, and DTO mapping. Knows how to talk to the API but nothing about MQTT or Home Assistant.
- **Domain** — forecast models, enrichment features (consensus, alerts, trends, derived values, indices, energy, history), and analysis logic. Pure domain, no I/O concerns.
- **Egress** — MQTT publishing, Home Assistant discovery payload construction, topic scheme, and availability management. Knows MQTT but nothing about Open-Meteo.

Ingest and Egress never reference each other. All data flows through the domain model.

## Streaming Pipeline

The poll pipeline is an Akka.Streams graph that runs on each tick:

<likec4-view view-id="pipeline"></likec4-view>

1. **TickSource** fires at the configured `PollInterval` (default: 60 minutes)
2. **FanOut** expands the tick into one request per location–model pair
3. **Throttle** rate-limits requests to stay within the Open-Meteo API budget
4. **HTTP** executes the API calls via `OpenMeteoClient`
5. **Aggregate** groups responses by poll cycle with timeout and quorum
6. **Enrich** runs the enrichment pipeline (consensus, alerts, etc.)
7. **MQTT Sink** publishes state and discovery payloads to the broker

Actors own connection lifecycle (MQTT connect/disconnect, Home Assistant birth-message handling) while streams handle the data flow. This separation keeps the pipeline testable and the lifecycle management isolated.
