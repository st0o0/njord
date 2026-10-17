# MQTT Reference

::: info
MQTT is an **optional** egress path, disabled by default. The primary way to connect njord to Home Assistant is the [included custom integration](/home-assistant/), which uses gRPC streaming for real-time updates. Enable MQTT only if you need to consume njord data from non-HA systems such as Node-RED, Grafana, or custom dashboards. See [MQTT configuration](/configuration/mqtt) to enable it.
:::

This page documents the MQTT state topics, payload format, and availability mechanism used by njord. njord publishes state only; it does not publish MQTT Discovery payloads, so Home Assistant does not create entities from these topics.

## Topic scheme

All topics use the configured `BaseTopic` (default: `njord`). Locations appear as a slug of the configured name.

### Status (LWT)

```
{baseTopic}/status
```

Payload: `online` or `offline` (Last Will and Testament). Published as retained.

### Forecast state (hourly)

```
{baseTopic}/{location}/{modelId}/h{hours}
```

Examples:
```
njord/berlin/icon_d2/h3
njord/berlin/icon_eu/h24
njord/amsterdam/gfs_seamless/h72
```

Each topic receives a retained JSON payload with all parameter values for that model, location, and horizon.

### Forecast state (daily)

```
{baseTopic}/{location}/{modelId}/d{offset}
```

Examples:
```
njord/berlin/icon_eu/d0     # today
njord/berlin/icon_eu/d1     # tomorrow
njord/berlin/icon_eu/d15    # 15 days out
```

Daily offsets range from `d0` (today) to `d{ForecastDays - 1}`.

### Enrichment topics

Analysis features publish to their own sub-topics under the location:

| Feature | Topic pattern | Example |
|---------|---------------|---------|
| Consensus | `{baseTopic}/{location}/consensus/h{N}` and `/d{offset}` | `njord/berlin/consensus/h3` |
| Alerts | `{baseTopic}/{location}/alerts/{alertType}` (one topic per active alert) | `njord/berlin/alerts/frost` |
| Derived | `{baseTopic}/{location}/derived/h{N}` and `/meta` | `njord/berlin/derived/h3` |
| Trends | `{baseTopic}/{location}/trends` | `njord/berlin/trends` |
| Indices | `{baseTopic}/{location}/indices/d{offset}` | `njord/berlin/indices/d0` |

## Payload format

### Hourly forecast payload

Each hourly state topic (`h{N}`) contains a JSON object with all configured parameter values:

```json
{
  "temperature_2m": 18.5,
  "apparent_temperature": 16.2,
  "relative_humidity_2m": 72,
  "dew_point_2m": 13.1,
  "precipitation": 0.0,
  "rain": 0.0,
  "weather_code": 3,
  "cloud_cover": 85,
  "pressure_msl": 1013.2,
  "wind_speed_10m": 4.2,
  "wind_direction_10m": 225,
  "wind_gusts_10m": 8.1,
  "precipitation_probability": 15,
  "visibility": 24000,
  "is_day": 1
}
```

Only parameters that have a value for the given model and horizon are included. Missing values are omitted (not null). This applies to the forecast (per-model and daily) payloads only; the consensus, alert, derived, trend and index payloads below write a member that is known but has no value as an explicit `null`.

### Daily forecast payload

```json
{
  "temperature_2m_max": 22.4,
  "temperature_2m_min": 12.1,
  "weather_code": 61,
  "precipitation_sum": 3.2,
  "wind_speed_10m_max": 6.8,
  "sunrise": "2026-07-15T05:32",
  "sunset": "2026-07-15T21:45",
  "daylight_duration": 58380
}
```

### Consensus payload

Each parameter carries its median and the number of models that contributed; `_spread` and `_agreement` describe the first parameter of the topic:

```json
{
  "temperature_2m": 18.3,
  "temperature_2m_models": 5,
  "wind_speed_10m": 4.0,
  "wind_speed_10m_models": 5,
  "_spread": 2.1,
  "_agreement": 0.85
}
```

### Alert payload

One retained payload per active alert type (here `njord/berlin/alerts/frost`):

```json
{
  "severity": "yellow",
  "confidence": 0.8,
  "trigger_value": -1.2,
  "threshold": 0,
  "peak_value": null,
  "hours_until": 9,
  "duration_hours": null,
  "expected_low": -1.2,
  "earliest_frost": "+9h",
  "models_agreeing": 4
}
```

Every alert payload starts with `severity`, `confidence`, `trigger_value`, `threshold`, `peak_value`, `hours_until` and `duration_hours`, followed by the members of the alert type. `peak_value`, `hours_until` and `duration_hours` (and any other member without a value, such as `earliest_frost`, `freezing_level`, `exposure_risk` or `comfort_level`) are `null` when absent. In the ice alert, `soil_frozen` is a JSON boolean (`true` or `false`), `earliest_frost` in the frost alert stays text (`"+9h"`).

## Availability

njord publishes its availability on `{baseTopic}/status` (retained):

- `online`: published after every successful connect
- `offline`: registered as Last Will and Testament, so the broker publishes it if njord disconnects unexpectedly; njord also publishes it when the connection actor stops

State topics carry no expiry. Consumers should combine the status topic with the age of the last state message.

## Retained messages

All state payloads are published as **retained**. This means:
- New MQTT subscribers immediately receive the last known state
- A payload is only re-published when its content changed since the last publish
- Retained topics of removed locations or models stay on the broker until cleared manually
