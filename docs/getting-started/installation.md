# Installation

njord runs as a single Docker container. No external database, no API key.

## Docker Compose

Create a `docker-compose.yml` for a single location:

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
      - Njord__Locations__0__Name=Lucerne
      - Njord__Locations__0__Latitude=47.05
      - Njord__Locations__0__Longitude=8.31

volumes:
  njord-data:
```

```bash
docker compose up -d
```

Replace the location name and coordinates with your own. Right-click a spot on Google Maps to copy coordinates.

## What happens on startup

1. njord reads its configuration (environment variables or `appsettings.json`)
2. For each location, it starts a **PollWorker** that learns the optimal poll cycle per domain
3. The first poll fetches weather data from Open-Meteo and evaluates the enabled feature rules
4. Results are available on the gRPC port (default 8081)

## Data volume

The `/app/data` volume stores a SQLite journal with each PollWorker's learned schedule and API usage counters. Keep it mounted so state survives restarts.

## Multiple locations

Add more locations by incrementing the index:

```yaml
environment:
  - Njord__Locations__0__Name=Lucerne
  - Njord__Locations__0__Latitude=47.05
  - Njord__Locations__0__Longitude=8.31
  - Njord__Locations__1__Name=Zurich
  - Njord__Locations__1__Latitude=47.38
  - Njord__Locations__1__Longitude=8.54
```

Each location gets its own set of entities in Home Assistant.

::: tip
For complex setups with many features or locations, use the [Config Builder](/builder) to generate the configuration interactively.
:::

## Docker run

If you prefer `docker run`:

```bash
docker run -d \
  --name njord \
  --restart unless-stopped \
  -p 8081:8081 \
  -v njord-data:/app/data \
  -e Njord__Locations__0__Name=Lucerne \
  -e Njord__Locations__0__Latitude=47.05 \
  -e Njord__Locations__0__Longitude=8.31 \
  ghcr.io/st0o0/njord:latest
```

## Environment variables

All settings use double-underscore notation (`Njord__Section__Key`). Array items use zero-based indices. See [Configuration](/configuration/) for all options.

## Next

- [Connect Home Assistant](/getting-started/home-assistant) -- install ha-njord and add the integration
