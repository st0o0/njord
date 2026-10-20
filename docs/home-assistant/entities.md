# Entities

The integration creates entities across five platforms: `weather`, `sensor`, `binary_sensor`, `event`, and `button`.

## Weather (`weather.*`)

Each location creates one weather entity from the Ensemble Mean forecast of the Weather domain.

::: warning ha-njord update pending
The server side is multi-domain (Weather, Air Quality, Flood, Solar), served by `GetDomainData` and `StreamDomainData`. The ha-njord integration is being updated to match, so the entity set below may change. Air quality and flood values are available over gRPC.
:::

**State attributes (current conditions):**

| Attribute | Unit | Source |
|-----------|------|--------|
| `state` | condition string | WMO weather code mapped to HA condition (sunny, cloudy, rainy, etc.) |
| `temperature` | °C | Air temperature at 2m |
| `apparent_temperature` | °C | Feels-like temperature |
| `humidity` | % | Relative humidity at 2m |
| `pressure` | hPa | Mean sea level pressure |
| `wind_speed` | m/s | Wind speed at 10m |
| `wind_bearing` | ° | Wind direction (0=N, 90=E, 180=S, 270=W) |
| `cloud_cover` | % | Total cloud cover |

**Forecasts** are available via the `weather.get_forecasts` service with `type: hourly` or `type: daily`.

## Air quality, flood and solar data

The server delivers these domains per location alongside the weather forecast:

| Domain | Values |
|--------|--------|
| Air Quality | `pm25`, `pm10`, `ozone`, `birch_pollen`, `grass_pollen`, `alder`, `mugwort` |
| Flood | `discharge` with mean, median, min, max, P25 and P75 |
| Solar | `direct_radiation`, `diffuse_radiation`, `shortwave_radiation`, `sunshine_duration` (needs an enrichment that requests solar variables on the server) |

Air quality and flood alerts are computed on the server and become available to Home Assistant once they are exposed over gRPC.

---

## Alert sensors (`sensor.*_alert`)

14 alert types, each showing the actual trigger value as state. **Disabled by default.**

| Alert Type | Entity suffix | Unit | What it measures |
|------------|---------------|------|------------------|
| Frost | `frost_alert` | °C | Minimum forecast temperature |
| Heat | `heat_alert` | °C | Maximum forecast temperature |
| Storm | `storm_alert` | m/s | Maximum wind gust speed |
| Heavy Rain | `heavy_rain_alert` | mm | Precipitation amount |
| UV | `uv_alert` | UV | Maximum UV index |
| Fog | `fog_alert` | m | Minimum visibility |
| Snow | `snow_alert` | cm | Snowfall depth |
| Pressure Drop | `pressure_drop_alert` | hPa | Pressure change (3h) |
| Thunderstorm | `thunderstorm_alert` | m/s | Wind gusts during thunderstorm |
| Ice | `ice_alert` | °C | Surface temperature near freezing |
| Wind Chill | `wind_chill_alert` | °C | Perceived cold temperature |
| Visibility | `visibility_alert` | m | Low visibility distance |
| Tropical Night | `tropical_night_alert` | °C | Minimum nighttime temperature |
| Humidity | `humidity_alert` | % | Extreme humidity levels |

Entity ID pattern: `sensor.njord_{location}_{type}_alert` (e.g. `sensor.njord_home_frost_alert`)

State is `None` when no alert data is available. The trigger value (e.g. -2.1 °C for frost) is shown when an alert condition exists.

**Attributes:**

| Attribute | Type | Description |
|-----------|------|-------------|
| `severity` | string | Alert level: `none`, `yellow`, `orange`, `red` |
| `confidence` | float | Certainty (0 to 1) |
| `threshold` | float | Configured threshold for this alert type |
| `peak_value` | float | Maximum forecast value if higher than current (only when set) |
| `hours_until` | int | Hours until the condition occurs (only when set) |
| `duration_hours` | int | Expected duration in hours (only when set) |

These attributes come from the gRPC API.

---

## Diagnostic sensors (server device)

| Entity | Unit | Description |
|--------|------|-------------|
| `sensor.njord_monthly_usage` | % | Monthly API budget usage |
| `sensor.njord_daily_usage` | % | Daily API budget usage |
| `sensor.njord_version` | string | njord server version |
| `sensor.njord_uptime` | h | Server uptime |

Each usage sensor carries `limit` and `used` attributes.

**Target sensors** (disabled by default): `sensor.njord_{location}_target` shows the next poll time for each location, with attributes `phase`, `miss_count`, `last_change`, `cycle_seconds`.

---

## Binary sensors (`binary_sensor.*`)

| Entity | Default | Description |
|--------|---------|-------------|
| `binary_sensor.njord_forecast_stream` | enabled | gRPC domain data stream connectivity (diagnostic) |
| `binary_sensor.njord_enrichment_stream` | enabled | gRPC enrichment stream connectivity (diagnostic) |

---

## Event entity (`event.*`)

One event entity per location fires on weather alert state changes.

Entity ID: `event.njord_{location}_weather_alert`

| Event type | When it fires |
|------------|---------------|
| `alert_started` | A new alert activates |
| `alert_escalated` | An active alert increases in severity |
| `alert_deescalated` | An active alert decreases in severity |
| `alert_cleared` | An alert is no longer active |

**Event data:** `type`, `location`, `severity`, `confidence`, `trigger_value`, `threshold`, `peak_value`, `hours_until`, `duration_hours`, `previous_severity` (on escalation/deescalation/cleared)

---

## Button (`button.*`)

| Entity | Description |
|--------|-------------|
| `button.njord_trigger_poll` | Triggers an immediate poll cycle on the njord server |

Attributes after pressing: `triggered_count`, `last_triggered`.

---

## Dashboard examples

### Weather alerts overview

```yaml
type: entities
title: Weather Alerts
entities:
  - entity: sensor.njord_home_frost_alert
    name: Frost
  - entity: sensor.njord_home_heat_alert
    name: Heat
  - entity: sensor.njord_home_storm_alert
    name: Storm
  - entity: sensor.njord_home_uv_alert
    name: UV
  - entity: sensor.njord_home_thunderstorm_alert
    name: Thunderstorm
```

## Automation examples

### Frost warning using the event entity

```yaml
automation:
  - alias: "Frost warning notification"
    trigger:
      - platform: event
        event_type: state_changed
        event_data:
          entity_id: event.njord_home_weather_alert
    condition:
      - condition: template
        value_template: >
          {{ trigger.event.data.new_state.attributes.event_type == 'alert_started'
             and trigger.event.data.new_state.attributes.type == 'frost' }}
    action:
      - action: notify.mobile_app
        data:
          title: "Frost Warning"
          message: >
            Frost expected in {{ trigger.event.data.new_state.attributes.hours_until }} hours.
            Trigger value: {{ trigger.event.data.new_state.attributes.trigger_value }} °C
```

### Trigger a poll on demand

```yaml
automation:
  - alias: "Poll njord before sunrise"
    trigger:
      - platform: sun
        event: sunrise
        offset: "-01:00:00"
    action:
      - action: button.press
        target:
          entity_id: button.njord_trigger_poll
```
