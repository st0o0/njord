# Home Assistant

njord integrates with Home Assistant via [MQTT Discovery](https://www.home-assistant.io/integrations/mqtt/#mqtt-discovery). After the first poll cycle, devices and sensors appear automatically.

## How devices appear

Each weather model per location creates a separate device in Home Assistant:

- **Device name:** `njord {location} {model}` (e.g. "njord berlin icon_d2")
- **Manufacturer:** njord
- **Model:** the weather model ID or enrichment type

Enrichment features create additional devices:
- `njord {location} consensus`
- `njord {location} alerts`
- `njord {location} derived`
- etc.

Find all njord devices under **Settings > Devices & Services > MQTT**.

## Entity naming

### Sensors (hourly forecasts)

```
sensor.njord_{location}_{model}_{parameter}_{horizon}
```

Examples:
```
sensor.njord_berlin_icon_d2_temperature_2m_h3
sensor.njord_berlin_icon_eu_wind_speed_10m_h24
sensor.njord_berlin_ecmwf_ifs025_precipitation_h48
```

The display name uses a shorter format: `{parameter} +{hours}h` (e.g. "temperature_2m +3h").

### Sensors (daily forecasts)

```
sensor.njord_{location}_{model}_{parameter}_{day}
```

Examples:
```
sensor.njord_berlin_icon_eu_temperature_2m_max_d0
sensor.njord_berlin_icon_eu_precipitation_sum_d1
sensor.njord_berlin_icon_eu_sunrise_d0
```

### Alert binary sensors

```
binary_sensor.njord_{location}_alerts_{alerttype}
```

Examples:
```
binary_sensor.njord_berlin_alerts_frost
binary_sensor.njord_berlin_alerts_heat
binary_sensor.njord_berlin_alerts_storm
```

## Recorder exclude

njord creates many sensors per model device (250+ with default settings). To prevent your Home Assistant database from growing rapidly, add a recorder exclude:

```yaml
# configuration.yaml
recorder:
  exclude:
    entity_globs:
      - sensor.njord_*
      - binary_sensor.njord_*
```

::: tip
This excludes njord sensors from the long-term history database while keeping them fully functional for automations, dashboards, and current state display. If you want history for specific sensors, use an `include` override.
:::

## Dashboard examples

### Current temperature comparison card

Compare temperature forecasts across models using an entities card:

```yaml
type: entities
title: Temperature +3h — All Models
entities:
  - entity: sensor.njord_berlin_icon_d2_temperature_2m_h3
    name: ICON-D2
  - entity: sensor.njord_berlin_icon_eu_temperature_2m_h3
    name: ICON-EU
  - entity: sensor.njord_berlin_ecmwf_ifs025_temperature_2m_h3
    name: ECMWF
  - entity: sensor.njord_berlin_gfs_seamless_temperature_2m_h3
    name: GFS
  - entity: sensor.njord_berlin_consensus_temperature_2m_h3
    name: Consensus
```

### Forecast timeline card

Show how a parameter evolves across horizons:

```yaml
type: entities
title: Temperature Forecast — ICON-EU
entities:
  - entity: sensor.njord_berlin_icon_eu_temperature_2m_h3
    name: +3h
  - entity: sensor.njord_berlin_icon_eu_temperature_2m_h6
    name: +6h
  - entity: sensor.njord_berlin_icon_eu_temperature_2m_h12
    name: +12h
  - entity: sensor.njord_berlin_icon_eu_temperature_2m_h24
    name: +24h
  - entity: sensor.njord_berlin_icon_eu_temperature_2m_h48
    name: +48h
  - entity: sensor.njord_berlin_icon_eu_temperature_2m_h72
    name: +72h
```

### Weather alerts card

```yaml
type: entities
title: Weather Alerts
entities:
  - entity: binary_sensor.njord_berlin_alerts_frost
    name: Frost
  - entity: binary_sensor.njord_berlin_alerts_heat
    name: Heat
  - entity: binary_sensor.njord_berlin_alerts_storm
    name: Storm
  - entity: binary_sensor.njord_berlin_alerts_heavy_rain
    name: Heavy Rain
  - entity: binary_sensor.njord_berlin_alerts_thunderstorm
    name: Thunderstorm
```

### Automation example

Trigger an automation when a frost alert fires:

```yaml
automation:
  - alias: "Frost warning notification"
    trigger:
      - platform: state
        entity_id: binary_sensor.njord_berlin_alerts_frost
        to: "on"
    action:
      - service: notify.mobile_app
        data:
          title: "Frost Warning"
          message: >
            Frost expected. Lowest temperature:
            {{ state_attr('binary_sensor.njord_berlin_alerts_frost', 'expected_low') }} C
```

## Sensor availability

Sensors become unavailable when:
- njord disconnects (LWT sets `njord/status` to `offline`)
- No update arrives within 2x the poll interval (`expire_after`)
- A model does not provide data at a given horizon (the sensor simply never updates)

Both the LWT and per-sensor expiry must pass for a sensor to show as available.
