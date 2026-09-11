<p align="center">
  <img src="docs/public/logo.svg" width="120" alt="njord" />
</p>

<h1 align="center">njord</h1>

<p align="center">
  Multi-model weather intelligence for Home Assistant, powered by Open-Meteo
</p>

<p align="center">
  <a href="https://github.com/st0o0/njord/blob/main/LICENSE"><img src="https://img.shields.io/badge/license-MIT-blue" alt="License" /></a>
  <img src="https://img.shields.io/badge/.NET-10-512bd4" alt=".NET 10" />
  <a href="https://st0o0.github.io/njord/"><img src="https://img.shields.io/badge/docs-st0o0.github.io%2Fnjord-2563eb" alt="Docs" /></a>
</p>

---

A .NET service (Docker container) built on [Akka.NET](https://getakka.net/) + Akka.Streams that polls
the [Open-Meteo API](https://open-meteo.com/en/docs) for multiple weather models
per location, enriches the data through a configurable pipeline, and streams
everything to Home Assistant via the
[ha-njord](https://github.com/st0o0/ha-njord) custom integration (gRPC).
MQTT publishing is available as an optional alternative for non-HA consumers.

## Features

- **50+ weather models.** ICON, ECMWF, GFS, UKMO, MeteoSwiss, and regional models from Open-Meteo.
- **Multiple locations.** Configure as many locations as you need, each with its own model selection.
- **Enrichment pipeline.** Consensus forecasts, weather alerts, derived values (Beaufort, wind chill, comfort), trend analysis, activity indices, and forecast accuracy tracking.
- **Hourly and daily forecasts.** Configurable horizons for hourly data, plus daily min/max, precipitation sums, sunrise/sunset.
- **External sensor input.** Feed indoor temperature/humidity from Home Assistant sensors via gRPC to improve index calculations.
- **Native HA integration.** The [ha-njord](https://github.com/st0o0/ha-njord) custom integration connects via gRPC streaming for real-time updates, native `weather` entities, alert events, activity indices, and more.
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

### 2. Install ha-njord in Home Assistant

Install via [HACS](https://hacs.xyz/) (search for "njord Weather") or copy `custom_components/njord` manually from [ha-njord](https://github.com/st0o0/ha-njord). Restart Home Assistant.

### 3. Add the integration

Go to **Settings > Devices & Services > Add Integration**, search for **njord Weather**, and enter the host and gRPC port (default: 8081). The integration auto-discovers all locations and models and creates weather entities, alert sensors, activity indices, and more.

## Documentation

Full documentation is available at **[st0o0.github.io/njord](https://st0o0.github.io/njord/)**:

- [Getting Started](https://st0o0.github.io/njord/getting-started): installation and setup with ha-njord
- [Configuration](https://st0o0.github.io/njord/configuration/): all available options
- [Model Catalog](https://st0o0.github.io/njord/models): choosing the right weather models
- [Home Assistant](https://st0o0.github.io/njord/home-assistant): entities, dashboards, automations
- [Architecture](https://st0o0.github.io/njord/architecture): system design and data flow
- [Config Builder](https://st0o0.github.io/njord/builder): interactive configuration generator
- [ha-njord](https://github.com/st0o0/ha-njord): the HA custom integration (entity reference, installation)

## Build & Test

All commands run from `src/`:

```powershell
dotnet build Njord.slnx
dotnet run --project Njord.Tests/Njord.Tests.csproj   # xUnit v3 via MTP
```

## License

[MIT](LICENSE). Weather data from [Open-Meteo](https://open-meteo.com/) is licensed under [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/).
