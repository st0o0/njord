# MQTT Reference

::: info
MQTT is an **optional** output, disabled by default. The primary way to connect njord to Home Assistant is the [custom integration](/home-assistant/) over gRPC. Enable MQTT only for non-HA consumers such as Node-RED, Grafana or custom dashboards. See [MQTT configuration](/configuration/mqtt).
:::

njord publishes retained state only. It does not publish MQTT Discovery payloads, so Home Assistant creates no entities from these topics.

## Topic scheme

All topics start with the configured `BaseTopic` (default `njord`). The location is the slug of its configured name (`Lucerne` becomes `lucerne`).

### Status

```
{baseTopic}/status
```

`online` or `offline` (retained, Last Will and Testament).

### Domain data

```
{baseTopic}/{location}/{domain}/state            # full JSON payload of the first forecast point
{baseTopic}/{location}/{domain}/{parameter}      # one scalar value
```

| Domain segment | Source |
|----------------|--------|
| `weather` | Ensemble Mean |
| `air-quality` | Air Quality |
| `flood` | Flood |
| `solar` | Solar variables of the Weather fetch (published when an enrichment requests solar variables) |
| `marine` | Marine (payload defined; not fetched by default) |

Examples:

```
njord/lucerne/weather/state
njord/lucerne/weather/temperature
njord/lucerne/air-quality/pm25
njord/lucerne/flood/discharge
njord/lucerne/solar/direct_radiation
```

Scalar topics are only published for values the source delivered. All payloads are retained and re-published only when their content changed.

### Alert topics

```
{baseTopic}/{location}/alerts/{type}
```

Types: `frost`, `heat`, `storm`, `heavy-rain`, `uv`, `fog`, `snow`, `pressure-drop`, `thunderstorm`, `ice`, `wind-chill`, `visibility`, `tropical-night`, `humidity`.

Example: `njord/lucerne/alerts/frost`. Air quality and flood alerts are not published over MQTT yet.

## Payloads

JSON keys are snake_case. Every domain payload starts with `valid_from` and `model_run` (ISO 8601). Optional members that a source does not deliver are omitted.

### Weather

```json
{
  "valid_from": "2026-10-19T12:00:00+00:00",
  "model_run": "2026-10-19T06:00:00+00:00",
  "temperature": 14.2,
  "apparent_temperature": 12.8,
  "wind_speed": 3.1,
  "precipitation": 0.0,
  "precipitation_probability": 15,
  "cloud_cover": 85,
  "pressure": 1013.2,
  "weather_code": 3,
  "humidity": 72,
  "dew_point": 9.1,
  "uv_index": 2.4,
  "ensemble_std_dev": 1.2,
  "wind_gusts": 7.8
}
```

Optional members: `ensemble_std_dev`, `wind_gusts`, `visibility`, `cape`, `rain`, `snowfall`, `soil_temperature`, `freezing_level_height`, `is_day`. `ensemble_std_dev` is the forecast spread of the ensemble.

### Air quality

```json
{
  "valid_from": "2026-10-19T12:00:00+00:00",
  "model_run": "2026-10-19T06:00:00+00:00",
  "pm25": 12.3,
  "pm10": 24.5,
  "ozone": 85.0,
  "birch_pollen": 15.0,
  "grass_pollen": 3.0
}
```

Optional members: `birch_pollen`, `grass_pollen`, `alder`, `mugwort` (pollen is only delivered in season and region).

### Flood

```json
{
  "valid_from": "2026-10-19T00:00:00+00:00",
  "model_run": "2026-10-19T00:00:00+00:00",
  "discharge": 145.2,
  "discharge_mean": 140.0,
  "discharge_median": 130.0,
  "discharge_max": 210.0,
  "discharge_min": 95.0,
  "discharge_p25": 118.0,
  "discharge_p75": 165.0
}
```

`discharge` is river discharge in m3/s; the other members are optional ensemble and historical statistics.

### Solar

```json
{
  "valid_from": "2026-10-19T12:00:00+00:00",
  "model_run": "2026-10-19T06:00:00+00:00",
  "direct_radiation": 450.0,
  "diffuse_radiation": 120.0,
  "shortwave_radiation": 570.0,
  "sunshine_duration": 3600.0
}
```

### Alert

One retained payload per alert type, for example `njord/lucerne/alerts/frost`:

```json
{
  "severity": "yellow",
  "confidence": 0.85,
  "trigger_value": -1.2,
  "threshold": 0.0,
  "peak_value": -1.2,
  "hours_until": 9,
  "duration_hours": 4,
  "expected_low": -1.2,
  "earliest_frost": "+9h",
  "models_agreeing": 5
}
```

Common members: `severity` (`none`, `yellow`, `orange`, `red`), `confidence` (0 to 1, derived from the ensemble standard deviation), `trigger_value`, `threshold`, and `peak_value`, `hours_until`, `duration_hours` (explicit `null` when not set). Each alert type adds its own members after these (for example `expected_low` for frost). The ice alert's `soil_frozen` is a JSON boolean.

## Availability

`{baseTopic}/status` carries `online` after every successful connect and `offline` as Last Will when the connection drops. State topics carry no expiry; combine the status topic with the age of the last state message.

## Retained messages

- New subscribers immediately receive the last known state
- A payload is re-published only when its content changed, and again after every broker reconnect
- Retained topics of removed locations stay on the broker until cleared manually
