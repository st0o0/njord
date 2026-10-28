# Entities

The integration creates entities across six platforms: `weather`, `sensor`, `binary_sensor`, `calendar`, `event`, and `button`.

## Weather (`weather.*`)

Each location creates one weather entity from the high-resolution deterministic forecast (Forecast API, `api.open-meteo.com`). When Model Comparison is enabled, additional weather entities appear for each configured ensemble model.

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

## Domain data sensors

The server delivers data for each enabled domain per location:

| Domain | Sensors | Examples |
|--------|---------|---------|
| Air Quality | PM2.5, PM10, ozone, pollen (birch, grass, alder, mugwort, ragweed), per-pollutant AQI | `sensor.lucerne_pm2_5`, `sensor.lucerne_birch_pollen` |
| Flood | River discharge | `sensor.lucerne_river_discharge` |
| Lifestyle | Commute safety, exercise, soil moisture, evapotranspiration, growing degree | `sensor.lucerne_commute_safety` |
| Energy | Solar yield, elevated wind speed/direction | `sensor.lucerne_solar_yield` |
| Scores | Allergy severity, sleep quality, asthma risk, spray window | `sensor.lucerne_allergy_severity`, `sensor.lucerne_sleep_quality` |
| Conditions | Heat pump COP, solar battery strategy, irrigation need, frost depth, inversion detection, atmospheric stability, cloud ceiling | `sensor.lucerne_heat_pump_cop`, `sensor.lucerne_cloud_ceiling` |

The set of sensors depends on which features are enabled. The entity count for a single location ranges from about 45 (defaults) to about 70 (all features).

---

## Alert binary sensors (`binary_sensor.*_alert`)

Up to 30 alert types as binary sensors. Active alerts show `on`, inactive show `off`. The active alert's severity and details are exposed as state attributes.

### Weather alerts (22)

| Alert | Entity suffix | What it warns about |
|-------|---------------|---------------------|
| Frost | `frost_alert` | Temperature below freezing |
| Heat | `heat_alert` | Extreme air or apparent temperature |
| Storm | `storm_alert` | Dangerous wind gusts |
| Heavy Rain | `heavy_rain_alert` | Intense precipitation |
| UV | `uv_alert` | High UV index |
| Fog | `fog_alert` | Fog-forming conditions |
| Snow | `snow_alert` | Significant snowfall |
| Pressure Drop | `pressure_drop_alert` | Rapid pressure change |
| Thunderstorm | `thunderstorm_alert` | Thunderstorm risk (CAPE + gusts) |
| Ice | `ice_alert` | Black ice risk |
| Wind Chill | `wind_chill_alert` | Dangerous wind chill |
| Visibility | `visibility_alert` | Low visibility |
| Tropical Night | `tropical_night_alert` | Night minimum above 20 °C |
| Humidity | `humidity_alert` | Oppressive dew point |
| Rain Probability | `rain_probability_alert` | High chance of rain in the next hours |
| Wet-Bulb Danger | `wet_bulb_danger_alert` | Heat stroke risk (wet-bulb temperature) |
| Clear-Sky UV | `clear_sky_uv_alert` | UV danger when clouds break |
| Shower | `shower_alert` | Sudden convective showers |
| Fire Risk | `fire_risk_alert` | Wildfire risk conditions |
| Coastal Flood | `coastal_flood_alert` | Storm surge risk |
| Compound Heat Stress | `compound_heat_stress_alert` | Combined heat, humidity and air quality stress |
| Migraine Risk | `migraine_risk_alert` | Weather conditions that commonly trigger migraines |

### Other alerts

| Alert | Entity suffix | What it warns about |
|-------|---------------|---------------------|
| Pollen | `pollen_alert` | High pollen concentration |
| PM2.5 | `pm2_5_alert` | Fine particulate matter above WHO guidelines |
| Dust | `dust_alert` | Saharan dust concentration |
| Smog Risk | `smog_risk_alert` | Low boundary layer + high PM2.5 |
| Pollen Blast | `pollen_blast_alert` | Pollen explosion after rain |
| Flood Risk | `flood_risk_alert` | River discharge above historical thresholds |
| Flood Rain | `flood_rain_alert` | Heavy rain + rising discharge |
| Swimming | `swimming_conditions_alert` | Unsafe wave or water conditions |

Entity ID pattern: `binary_sensor.{location}_{type}_alert`

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

## Calendar (`calendar.*`)

One calendar entity per location shows the activity time windows of the score features.

Entity ID: `calendar.{location}_activity_windows`

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

### Data and feature events

Two further event entities per location:

| Entity | Event types | When it fires |
|--------|-------------|---------------|
| `event.{location}_data` | `domain_updated`, `features_updated` | Data lifecycle: a domain received new data, or features were re-evaluated |
| `event.{location}_features` | `score_threshold`, `condition_changed`, window events | Feature transitions: a score crosses a threshold, a condition changes, an activity window opens or closes |

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
  - entity: binary_sensor.home_frost_alert
    name: Frost
  - entity: binary_sensor.home_heat_alert
    name: Heat
  - entity: binary_sensor.home_storm_alert
    name: Storm
  - entity: binary_sensor.home_uv_alert
    name: UV
  - entity: binary_sensor.home_thunderstorm_alert
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
