# Entities

The integration creates entities across five platforms: `weather`, `sensor`, `binary_sensor`, `event`, and `button`.

## Weather (`weather.*`)

Each location/model combination creates a weather entity. An additional consensus entity aggregates all models per location.

| Entity | Example |
|--------|---------|
| Per model | `weather.njord_home_icon_d2` |
| Consensus | `weather.njord_home_consensus` |

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

**Model info attributes** (on per-model entities):

| Attribute | Description |
|-----------|-------------|
| `model_display_name` | Human-readable model name |
| `model_provider` | Weather service provider |
| `model_region` | Geographic coverage area |
| `model_coverage_tier` | Coverage classification |
| `model_resolution_km` | Grid resolution in km |
| `model_max_forecast_hours` | Maximum forecast horizon |

When the history enrichment is active, per-model entities also carry `model_mae_7d`, `model_mae_30d`, `model_weight`, and `model_drift`.

**Consensus extra attributes:**

| Attribute | Description |
|-----------|-------------|
| `agreement` | Fraction of models agreeing on temperature direction (0 to 1) |
| `available_models` | Number of models contributing |
| `spread` | Temperature spread between models in °C |
| `reliable_hours` | Hours where model agreement stays above 50% |
| `current_horizon` | Which horizon offset is currently displayed (e.g. `h3`) |

**Forecasts** are available via the `weather.get_forecasts` service with `type: hourly` or `type: daily`. Extra forecast parameters configured in njord (e.g. `cape`, `uv_index`) appear as additional fields in the forecast entries.

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

---

## Activity indices (`sensor.*_index`)

8 activity scores (0 to 100%) indicating weather suitability. **Disabled by default.**

| Entity | Icon | What it measures |
|--------|------|------------------|
| `sensor.njord_{loc}_laundry_index` | tshirt | Outdoor laundry drying conditions |
| `sensor.njord_{loc}_outdoor_index` | pine-tree | General outdoor comfort |
| `sensor.njord_{loc}_running_index` | run | Running conditions |
| `sensor.njord_{loc}_cycling_index` | bike | Cycling conditions |
| `sensor.njord_{loc}_bbq_index` | grill | Barbecue-friendliness |
| `sensor.njord_{loc}_irrigation_index` | sprinkler | Garden irrigation need |
| `sensor.njord_{loc}_solar_index` | solar-power | Solar energy production potential |
| `sensor.njord_{loc}_night_ventilation_index` | air-filter | Night ventilation benefit |

Each index sensor carries a `forecast` attribute with an array of `{day_offset, score}` entries for multi-day outlook.

---

## Additional sensors

All **disabled by default**. Enable via the entity registry.

### Frost

| Entity | Unit | Description |
|--------|------|-------------|
| `sensor.njord_{loc}_frost_hours` | h | Forecast hours with temperature below 0 °C |
| `sensor.njord_{loc}_frost_confidence` | % | Confidence (0 to 100%) that frost will occur |

### VPD (Vapour Pressure Deficit)

| Entity | Unit | Description |
|--------|------|-------------|
| `sensor.njord_{loc}_vpd` | kPa | Drying power of the air. Attribute: `category` (low, optimal, high, critical) |

### Weather trend

| Entity | Description |
|--------|-------------|
| `sensor.njord_{loc}_weather_trend` | Weather change description (e.g. "clearing skies", "rain approaching") |

**Attributes:** `stability_label`, `precip_starts_in_hours`, `precip_ends_in_hours`, `temp_max_in_hours`, `temp_min_in_hours`, `reliable_hours`, `stability_ratio`, `decay_rate`, `parameter_trends`

### Derived metrics

| Entity | Unit | Description |
|--------|------|-------------|
| `sensor.njord_{loc}_sunshine` | % | Sunshine percentage based on cloud cover |
| `sensor.njord_{loc}_diurnal_amplitude` | °C | Daily max/min temperature difference |
| `sensor.njord_{loc}_beaufort` | 0 to 12 | Wind on the Beaufort scale |
| `sensor.njord_{loc}_wind_chill` | °C | Perceived temperature factoring wind |
| `sensor.njord_{loc}_dewpoint_comfort` | string | Comfort category based on dew point (e.g. "Comfortable", "Humid") |

### Model performance (diagnostic)

| Entity | Unit | Description |
|--------|------|-------------|
| `sensor.njord_{loc}_model_performance` | °C | Accuracy-weighted ensemble temperature |

**Attributes:** `models` (array of `{model, mae_7d, mae_30d, weight, drift}`), `seasonal_best`, `anomaly`, `anomaly_deviation`

---

## Diagnostic sensors (server device)

| Entity | Unit | Description |
|--------|------|-------------|
| `sensor.njord_monthly_usage` | % | Monthly API budget usage |
| `sensor.njord_daily_usage` | % | Daily API budget usage |
| `sensor.njord_version` | string | njord server version |
| `sensor.njord_uptime` | h | Server uptime |

Each usage sensor carries `limit` and `used` attributes.

**Target sensors** (disabled by default): `sensor.njord_{location}_{model}_target` shows the next poll time for each location/model pair, with attributes `phase`, `miss_count`, `last_change`, `cycle_seconds`.

---

## Binary sensors (`binary_sensor.*`)

| Entity | Default | Description |
|--------|---------|-------------|
| `binary_sensor.njord_{loc}_inversion` | disabled | Temperature inversion detection (warm air above cold) |
| `binary_sensor.njord_forecast_stream` | enabled | gRPC forecast stream connectivity (diagnostic) |
| `binary_sensor.njord_enrichment_stream` | enabled | gRPC enrichment stream connectivity (diagnostic) |
| `binary_sensor.njord_config_stream` | enabled | gRPC config stream connectivity (diagnostic) |

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

### Weather forecast card

```yaml
type: weather-forecast
entity: weather.njord_home_consensus
forecast_type: daily
```

### Multi-model comparison

```yaml
type: entities
title: Current Temperature by Model
entities:
  - entity: weather.njord_home_icon_d2
    name: ICON-D2
  - entity: weather.njord_home_icon_eu
    name: ICON-EU
  - entity: weather.njord_home_ecmwf_ifs025
    name: ECMWF
  - entity: weather.njord_home_consensus
    name: Consensus
```

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

### Activity indices

```yaml
type: entities
title: Activity Scores
entities:
  - entity: sensor.njord_home_outdoor_index
    name: Outdoor
  - entity: sensor.njord_home_running_index
    name: Running
  - entity: sensor.njord_home_cycling_index
    name: Cycling
  - entity: sensor.njord_home_bbq_index
    name: BBQ
  - entity: sensor.njord_home_solar_index
    name: Solar
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
