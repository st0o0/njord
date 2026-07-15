# Getting Started

This guide gets njord running with Docker, publishing weather forecasts to your Home Assistant instance via MQTT.

## Prerequisites

- **Docker** (or Podman) on any Linux, macOS, or Windows host
- **Mosquitto** (or another MQTT broker) reachable from the Docker host — most Home Assistant setups already have the [Mosquitto add-on](https://github.com/home-assistant/addons/tree/master/mosquitto)
- **MQTT integration** enabled in Home Assistant (Settings > Devices & Services > MQTT)

## Minimal configuration

Create an `appsettings.json` file:

```json
{
  "Njord": {
    "Mqtt": {
      "Host": "192.168.1.100",
      "Username": "mqtt-user",
      "Password": "mqtt-pass"
    },
    "Locations": [
      {
        "Name": "Home",
        "Latitude": 47.05,
        "Longitude": 8.31
      }
    ],
    "Models": [
      "icon_eu",
      "ecmwf_ifs025"
    ]
  }
}
```

This configures njord to poll two weather models for a single location every 60 minutes (the default) and publish forecasts at horizons +3, +6, +12, +24, +48, and +72 hours.

::: tip
For complex setups with multiple locations, per-location models, and enrichment features, use the [Config Builder](/builder) to generate your configuration interactively.
:::

## Run with Docker

```bash
docker run -d \
  --name njord \
  --restart unless-stopped \
  -v ./appsettings.json:/app/appsettings.json:ro \
  -v njord-data:/app/data \
  ghcr.io/st0o0/njord:latest
```

The `/app/data` volume stores the SQLite journal used for persistence (forecast history, scheduler state). Keep it mounted so data survives container restarts.

### Using environment variables

You can also pass configuration entirely via environment variables instead of a config file. The convention is `Njord__Section__Key`:

```bash
docker run -d \
  --name njord \
  --restart unless-stopped \
  -v njord-data:/app/data \
  -e Njord__Mqtt__Host=192.168.1.100 \
  -e Njord__Mqtt__Username=mqtt-user \
  -e Njord__Mqtt__Password=mqtt-pass \
  -e Njord__Locations__0__Name=Home \
  -e Njord__Locations__0__Latitude=47.05 \
  -e Njord__Locations__0__Longitude=8.31 \
  -e Njord__Models__0=icon_eu \
  -e Njord__Models__1=ecmwf_ifs025 \
  ghcr.io/st0o0/njord:latest
```

## Verify it works

### MQTT Explorer

Connect [MQTT Explorer](https://mqtt-explorer.com/) to your broker and look for topics under `njord/` and `homeassistant/device/`. You should see:

- `njord/status` — `online`
- `njord/home/icon_eu/h3` — forecast state JSON for +3 hours
- `homeassistant/device/njord_home_icon_eu/config` — HA discovery payload

### mosquitto_sub

```bash
# Watch all njord topics
mosquitto_sub -h 192.168.1.100 -u mqtt-user -P mqtt-pass -t 'njord/#' -v

# Watch discovery payloads
mosquitto_sub -h 192.168.1.100 -u mqtt-user -P mqtt-pass -t 'homeassistant/device/#' -v
```

### Home Assistant

After the first poll cycle (up to 60 minutes), check Settings > Devices & Services > MQTT. You should see devices named `njord home icon_eu` and `njord home ecmwf_ifs025`, each with sensors for every parameter and horizon.

## Next steps

- [Configuration overview](/configuration/) — all available options
- [Model catalog](/models) — choosing the right weather models for your region
- [MQTT reference](/mqtt-reference) — topic scheme and payload format
- [Home Assistant integration](/home-assistant) — entity naming, dashboards, recorder tips
