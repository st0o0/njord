<p align="center">
  <img src="docs/public/logo.svg" width="120" alt="njord" />
</p>

<h1 align="center">njord</h1>

<p align="center">
  Multi-model weather intelligence for Home Assistant, powered by Open-Meteo
</p>

<p align="center">
  <a href="https://github.com/st0o0/njord/releases"><img src="https://img.shields.io/github/v/release/st0o0/njord?style=flat-square" alt="Release" /></a>
  <a href="https://github.com/st0o0/njord/pkgs/container/njord"><img src="https://img.shields.io/badge/ghcr.io-st0o0%2Fnjord-2496ED?style=flat-square&logo=docker&logoColor=white" alt="GHCR" /></a>
  <a href="https://github.com/st0o0/njord/blob/main/LICENSE"><img src="https://img.shields.io/badge/license-MIT-blue?style=flat-square" alt="License" /></a>
  <img src="https://img.shields.io/badge/.NET-10-512bd4?style=flat-square" alt=".NET 10" />
  <a href="https://st0o0.github.io/njord/"><img src="https://img.shields.io/badge/docs-st0o0.github.io%2Fnjord-2563eb?style=flat-square" alt="Docs" /></a>
</p>

---

The **njord weather service** is a .NET container built on
[Akka.NET](https://getakka.net/) + Akka.Streams that polls the
[Open-Meteo API](https://open-meteo.com/en/docs) for multiple weather models per
location and enriches forecasts through a configurable pipeline. It serves
consumers via gRPC (primary) and MQTT (optional).

The **[Home Assistant custom integration](https://github.com/st0o0/ha-njord)**
connects via gRPC streaming for real-time updates, creating native weather
entities, alert sensors, activity indices, and more.

## Features

- **50+ weather models.** ICON, ECMWF, GFS, UKMO, MeteoSwiss, and regional models from Open-Meteo.
- **Multiple locations.** Configure as many locations as you need, each with its own model selection.
- **Enrichment pipeline.** Consensus forecasts, weather alerts, derived values (Beaufort, wind chill, comfort), trend analysis, activity indices, and forecast accuracy tracking.
- **Hourly and daily forecasts.** Configurable horizons for hourly data, plus daily min/max, precipitation sums, sunrise/sunset.
- **External sensor input.** Feed indoor temperature/humidity from Home Assistant sensors via gRPC to improve index calculations.
- **Single container.** Runs on any Docker host with SQLite persistence by default, no external database needed.

## Architecture

<p align="center">
  <img src="docs/public/index.png" alt="njord architecture" />
</p>

## Quick Start

### 1. Run njord

```yaml
# docker-compose.yml
services:
  njord:
    image: ghcr.io/st0o0/njord:latest
    restart: unless-stopped
    ports:
      - "8081:8081"
    volumes:
      - njord-data:/app/data
    environment:
      - Njord__Locations__0__Name=home
      - Njord__Locations__0__Latitude=47.05
      - Njord__Locations__0__Longitude=8.31

volumes:
  njord-data:
```

```bash
docker compose up -d
```

### 2. Install the HA integration

See the **[ha-njord](https://github.com/st0o0/ha-njord)** repository for installation via HACS and setup instructions.

## Documentation

Full documentation is available at **[st0o0.github.io/njord](https://st0o0.github.io/njord/)**:

- [Getting Started](https://st0o0.github.io/njord/getting-started): installation and setup
- [Configuration](https://st0o0.github.io/njord/configuration/): all available options
- [Model Catalog](https://st0o0.github.io/njord/models): choosing the right weather models
- [Home Assistant](https://st0o0.github.io/njord/home-assistant/): entities, options, diagnostics
- [Architecture](https://st0o0.github.io/njord/architecture): system design and data flow
- [Config Builder](https://st0o0.github.io/njord/builder): interactive configuration generator

## Repository Structure

```
src/                         .NET service (Akka.NET + Akka.Streams)
protos/                      Protobuf definitions (shared with ha-njord)
docs/                        VitePress documentation site
```

The Home Assistant integration lives in a separate repo: **[st0o0/ha-njord](https://github.com/st0o0/ha-njord)**.

## Build & Test

From `src/`:

```powershell
dotnet build Njord.slnx
dotnet run --project Njord.Core.Tests/Njord.Core.Tests.csproj   # xUnit v3 via MTP; one project per library (see AGENTS.md for the run-all loop)
```

## License

[MIT](LICENSE). Weather data from [Open-Meteo](https://open-meteo.com/) is licensed under [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/).
