# njord

[![Release](https://img.shields.io/github/v/release/st0o0/njord?sort=semver&style=flat-square)](https://github.com/st0o0/njord/releases)
[![GHCR](https://img.shields.io/badge/ghcr.io-st0o0%2Fnjord-2496ED?style=flat-square&logo=docker&logoColor=white)](https://github.com/st0o0/njord/pkgs/container/njord)
[![License: MIT](https://img.shields.io/badge/License-MIT-green.svg?style=flat-square)](LICENSE)

Multi-model weather intelligence for Home Assistant. A .NET container built on [Akka.NET](https://getakka.net/) + Akka.Streams that polls the [Open-Meteo API](https://open-meteo.com/en/docs) for multiple weather models per location and enriches forecasts through a configurable pipeline. Serves consumers via gRPC. The **[Home Assistant custom integration](https://github.com/st0o0/ha-njord)** connects via gRPC streaming for real-time updates, creating native weather entities, alert sensors, activity indices, and more.

## Features

- **50+ weather models** -- ICON, ECMWF, GFS, UKMO, MeteoSwiss, and regional models from Open-Meteo
- **Multiple locations** -- configure as many locations as you need, each with its own model selection
- **Enrichment pipeline** -- consensus forecasts, weather alerts, derived values (Beaufort, wind chill, comfort), trend analysis, and activity indices
- **Hourly and daily forecasts** -- configurable horizons for hourly data, plus daily min/max, precipitation sums, sunrise/sunset
- **External sensor input** -- feed indoor temperature/humidity from Home Assistant sensors via gRPC to improve index calculations
- **Single container** -- runs on any Docker host with SQLite persistence by default, no external database needed

## Architecture

![njord architecture](docs/public/index.png)

## Quick start

```yaml
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

Install the HA integration from the **[ha-njord](https://github.com/st0o0/ha-njord)** repository (HACS or manual).

## Documentation

Full reference at **[njord.st0o0.net](https://njord.st0o0.net/)**:

- [Getting Started](https://njord.st0o0.net/getting-started): installation and setup
- [Configuration](https://njord.st0o0.net/configuration/): all available options
- [Model Catalog](https://njord.st0o0.net/models): choosing the right weather models
- [Home Assistant](https://njord.st0o0.net/home-assistant/): entities, options, diagnostics
- [Architecture](https://njord.st0o0.net/architecture): system design and data flow
- [Config Builder](https://njord.st0o0.net/builder): interactive configuration generator

## Image

- **Registry:** `ghcr.io/st0o0/njord`
- **Tags:** semantic version (`1.0.0`), `latest`
- **Architectures:** `linux/amd64`, `linux/arm64`, `linux/arm/v7`

## Development

From `src/`:

```powershell
dotnet build Njord.slnx
dotnet run --project Njord.Core.Tests/Njord.Core.Tests.csproj
```

Tests are xUnit v3 on Microsoft Testing Platform -- run with `dotnet run`, not `dotnet test`. See AGENTS.md for the run-all loop.

Commits follow [Conventional Commits](https://www.conventionalcommits.org/); releases and the GHCR image are cut automatically by release-please.

## License

[MIT](LICENSE). Weather data from [Open-Meteo](https://open-meteo.com/) is licensed under [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/). njord enriches and transforms the raw forecast data. Open-Meteo aggregates data from national weather services (DWD, ECMWF, NOAA, and [others](https://open-meteo.com/en/licence)) under their respective licences.
