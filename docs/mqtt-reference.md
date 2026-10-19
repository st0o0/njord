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

### Domain data (per domain)

```
{baseTopic}/{location}/{domain}/{parameter}
```

Examples:
```
njord/lucerne/weather/state
njord/lucerne/weather/temperature_2m
njord/lucerne/air-quality/pm25
njord/lucerne/flood/discharge
njord/lucerne/solar/direct_radiation
```

Each domain publishes retained JSON payloads with the latest forecast values for that location and domain.

### Alert topics

```
{baseTopic}/{location}/alerts/{alertType}
```

Examples:
```
njord/lucerne/alerts/frost
njord/lucerne/alerts/heat
njord/lucerne/alerts/storm
```

One retained payload per active alert type.

## Payload format

### Weather domain payload

The weather state topic contains the first hourly forecast point from the ensemble:

```json
{
  "temperature_2m": 18.5,
  "apparent_temperature": 16.2,
  "relative_humidity_2m": 72,
  "dew_point_2m": 13.1,
  "precipitation": 0.0,
  "weather_code": 3,
  "cloud_cover": 85,
  "pressure_msl": 1013.2,
  "wind_speed_10m": 4.2,
  "precipitation_probability": 15,
  "ensemble_std_dev": 1.2
}
```

The `ensemble_std_dev` field indicates forecast uncertainty from the ensemble spread.

### Air quality domain payload

```json
{
  "pm25": 12.3,
  "pm10": 24.5,
  "ozone": 85.0,
  "birch_pollen": 15.0,
  "grass_pollen": 3.0
}
```

### Flood domain payload

```json
{
  "discharge": 145.2,
  "discharge_p10": 120.0,
  "discharge_p90": 180.0,
  "median_historical": 130.0
}
```

### Solar domain payload

```json
{
  "direct_radiation": 450.0,
  "diffuse_radiation": 120.0,
  "shortwave_radiation": 570.0,
  "sunshine_duration": 3600.0
}
```

### Alert payload

One retained payload per active alert type (here `njord/lucerne/alerts/frost`):

```json
{
  "severity": "yellow",
  "confidence": "high",
  "peak_value": -1.2,
  "hours_until": 9,
  "duration_hours": 4
}
```

Alert fields: `severity` (none/yellow/orange/red), `confidence` (high/medium/low, derived from ensemble standard deviation), plus type-specific details.

## Availability

njord publishes its availability on `{baseTopic}/status` (retained):

- `online`: published after every successful connect
- `offline`: registered as Last Will and Testament, so the broker publishes it if njord disconnects unexpectedly; njord also publishes it when the connection actor stops

State topics carry no expiry. Consumers should combine the status topic with the age of the last state message.

## Retained messages

All state payloads are published as **retained**. This means:
- New MQTT subscribers immediately receive the last known state
- A payload is only re-published when its content changed since the last publish
- Retained topics of removed locations stay on the broker until cleared manually
