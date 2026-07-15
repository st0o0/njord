# MQTT Reference

This page documents the complete MQTT topic scheme, payload format, and availability mechanism used by njord.

## Topic scheme

All topics use the configured `BaseTopic` (default: `njord`) and `DiscoveryPrefix` (default: `homeassistant`).

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

### Device discovery

```
{discoveryPrefix}/device/{deviceId}/config
```

Examples:
```
homeassistant/device/njord_berlin_icon_d2/config
homeassistant/device/njord_berlin_consensus/config
```

Device ID format: `njord_{slug(location)}_{modelId}` (or `_{enrichmentType}` for enrichment devices).

### Enrichment topics

Enrichment features publish to their own sub-topics under the location:

| Feature | Topic pattern | Example |
|---------|---------------|---------|
| Consensus | `{baseTopic}/{location}/consensus/h{N}` | `njord/berlin/consensus/h3` |
| Alerts | `{baseTopic}/{location}/alerts` | `njord/berlin/alerts` |
| Derived | `{baseTopic}/{location}/derived/h{N}` | `njord/berlin/derived/h3` |
| Trends | `{baseTopic}/{location}/trends` | `njord/berlin/trends` |
| Indices | `{baseTopic}/{location}/indices` | `njord/berlin/indices` |
| Energy | `{baseTopic}/{location}/energy` | `njord/berlin/energy` |
| History | `{baseTopic}/{location}/history` | `njord/berlin/history` |

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

Only parameters that have a value for the given model and horizon are included. Missing values are omitted (not null).

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

```json
{
  "temperature_2m": 18.3,
  "temperature_2m_spread": 2.1,
  "temperature_2m_agreement": 0.85,
  "temperature_2m_models_used": 5,
  "wind_speed_10m": 4.0,
  "wind_speed_10m_spread": 1.8,
  "wind_speed_10m_agreement": 0.72,
  "wind_speed_10m_models_used": 5
}
```

### Alert payload

```json
{
  "frost": "ON",
  "frost_severity": "Yellow",
  "frost_confidence": 0.8,
  "frost_expected_low": -1.2,
  "frost_earliest": "2026-07-16T04:00",
  "heat": "OFF",
  "storm": "OFF"
}
```

## Availability

njord uses a dual availability mechanism:

1. **LWT topic** (`{baseTopic}/status`) — the broker publishes `offline` if njord disconnects unexpectedly
2. **expire_after** — set to 2x the poll interval on every sensor; if no update arrives within that window, Home Assistant marks the sensor as unavailable

Both conditions must be met for a sensor to show as available (availability mode `"all"`).

## Retained messages

All state and discovery payloads are published as **retained**. This means:
- New MQTT subscribers immediately receive the last known state
- Home Assistant gets device configs and current values on restart
- Stale retained configs are tombstoned (empty payload) when the configuration changes
