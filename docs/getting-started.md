# Getting Started

This guide gets njord running and connected to Home Assistant.

## Prerequisites

- **Docker** (or Podman) on any Linux, macOS or Windows host
- **Home Assistant** with [HACS](https://hacs.xyz/) installed

## 1. Run njord

Create a `docker-compose.yml` for a single location (Lucerne):

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

Weather data comes from the Open-Meteo deterministic Forecast API (`api.open-meteo.com`), which automatically picks the best available high-resolution model for your location. Alerts that need confidence also poll the Ensemble Mean API for spread data. There is no model to configure for the primary forecast. See [Weather](/configuration/weather) for forecast-days and solar options.

The `/app/data` volume stores the SQLite journal (each PollWorker's learned schedule and API usage). Keep it mounted so state survives restarts.

Each poll cycle fetches the domains the enabled features need per location. The defaults (18 features: 15 weather alerts incl. RainProbability + Commute + Pollen + PM2.5) need Forecast, Ensemble and Air Quality, about 3,600 API calls per month at the default 60-minute interval. See [Budget](/configuration/budget).

::: tip
For several locations or solar data, use the [Config Builder](/builder).
:::

## 2. Install the Home Assistant integration

### HACS (recommended)

[![Open your Home Assistant instance and open a repository inside the Home Assistant Community Store.](https://my.home-assistant.io/badges/hacs_repository.svg)](https://my.home-assistant.io/redirect/hacs_repository/?owner=st0o0&repository=ha-njord&category=integration)

1. Click the button above, or open HACS > three dots > **Custom repositories** and add `https://github.com/st0o0/ha-njord` as **Integration**
2. Search for "njord Weather" and install
3. Restart Home Assistant

### Manual

Copy `custom_components/njord` from the [ha-njord repository](https://github.com/st0o0/ha-njord) to your Home Assistant `config/custom_components/` directory and restart.

## 3. Add the integration

1. Go to **Settings > Devices & Services > Add Integration**
2. Search for **njord Weather**
3. Enter the host and gRPC port (default 8081) of your njord instance

## What you will see

Under **Settings > Devices & Services > njord Weather**:

- Per location: weather entity, air quality and flood sensors, alert binary sensors, lifestyle conditions, an event entity for alert state changes
- On the server device: a "Trigger Poll" button, API usage, version, uptime and stream connectivity sensors

With all defaults you get about 45 entities per location (about 70 with all features enabled). Enable more features to add lifestyle scores, energy monitoring, flood risk and more. See [Entities](/home-assistant/entities).

## How it works

<likec4-view view-id="index"></likec4-view>

njord polls Open-Meteo every 60 minutes by default, enriches the data with alerts and streams it to Home Assistant over gRPC. See [Architecture](/architecture).

## Environment variables

All settings can be passed as environment variables with double-underscore notation (`Njord__Section__Key`). Array items use zero-based indices:

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

## Next steps

- [Configuration overview](/configuration/)
- [Models](/models)
- [Home Assistant integration](/home-assistant/)
- [Config Builder](/builder)
