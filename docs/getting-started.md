# Getting Started

This guide gets njord running and connected to Home Assistant.

## Prerequisites

- **Docker** (or Podman) on any Linux, macOS, or Windows host
- **Home Assistant** with [HACS](https://hacs.xyz/) installed

## 1. Run njord

Create a `docker-compose.yml`:

```yaml
services:
  njord:
    image: ghcr.io/st0o0/njord:latest
    restart: unless-stopped
    volumes:
      - njord-data:/app/data
    environment:
      - Njord__Locations__0__Name=Home
      - Njord__Locations__0__Latitude=47.05
      - Njord__Locations__0__Longitude=8.31
      - Njord__Models__0=icon_eu
      - Njord__Models__1=ecmwf_ifs025

volumes:
  njord-data:
```

```bash
docker compose up -d
```

The `/app/data` volume stores the SQLite journal used for persistence (forecast history, scheduler state). Keep it mounted so data survives container restarts.

::: tip
For complex setups with multiple locations, per-location models, and enrichment features, use the [Config Builder](/builder) to generate your configuration interactively.
:::

## 2. Install the Home Assistant integration

### HACS (recommended)

[![Open your Home Assistant instance and open a repository inside the Home Assistant Community Store.](https://my.home-assistant.io/badges/hacs_repository.svg)](https://my.home-assistant.io/redirect/hacs_repository/?owner=st0o0&repository=ha-njord&category=integration)

1. Click the button above, or open HACS > three dots > **Custom repositories** > add `https://github.com/st0o0/ha-njord` as **Integration**
2. Search for "njord Weather" and install
3. Restart Home Assistant

### Manual

Copy the `custom_components/njord` directory from [ha-njord](https://github.com/st0o0/ha-njord) to your Home Assistant `config/custom_components/` directory and restart.

## 3. Add the integration

1. Go to **Settings > Devices & Services > Add Integration**
2. Search for **njord Weather**
3. Enter the host and gRPC port (default: 8081) of your njord instance
4. The integration connects via gRPC streaming and auto-discovers all locations and models

## Verify it works

After adding the integration, entities appear immediately with no polling delay. Check **Settings > Devices & Services > njord Weather**. You should see:

- One `weather` entity per model per location (e.g. `weather.njord_home_icon_eu`)
- One `weather` entity for the consensus forecast per location
- Alert, index, trend, and derived sensors per location
- A "Trigger Poll" button on the server device
- Stream connectivity sensors (diagnostic)

## How it works

<likec4-view view-id="index"></likec4-view>

njord polls the Open-Meteo API on a configurable interval (default: 60 minutes), processes forecasts through an enrichment pipeline, and streams the results to the Home Assistant integration via gRPC. The integration creates native HA entities that update in real-time. See the [Architecture](/architecture) page for a deeper look.

## Using environment variables

You can pass all configuration via environment variables using double-underscore notation (`Njord__Section__Key`). Array items use zero-based indices:

```bash
docker run -d \
  --name njord \
  --restart unless-stopped \
  -v njord-data:/app/data \
  -e Njord__Locations__0__Name=Home \
  -e Njord__Locations__0__Latitude=47.05 \
  -e Njord__Locations__0__Longitude=8.31 \
  -e Njord__Models__0=icon_eu \
  -e Njord__Models__1=ecmwf_ifs025 \
  ghcr.io/st0o0/njord:latest
```

## Alternative: MQTT

If you use njord without Home Assistant (e.g. with Node-RED or a custom dashboard), enable MQTT publishing:

```yaml
environment:
  - Njord__Mqtt__Enabled=true
  - Njord__Mqtt__Host=<your-mosquitto-host>
  - Njord__Mqtt__Username=mqtt-user
  - Njord__Mqtt__Password=mqtt-pass
```

See the [MQTT reference](/mqtt-reference) for the complete topic scheme and payload format.

## Next steps

- [Configuration overview](/configuration/): all available options
- [Model catalog](/models): choosing the right weather models for your region
- [Home Assistant integration](/home-assistant): entity reference, dashboards, automations
- [Config Builder](/builder): interactive configuration generator
