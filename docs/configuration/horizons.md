# Horizons

`Horizons` is a list of forecast offsets in hours.

```json
{
  "Njord": {
    "Horizons": [3, 6, 12, 24, 48, 72]
  }
}
```

The default is `[3, 6, 12, 24, 48, 72]`.

## Current behavior

In the multi-domain architecture njord publishes the forecast series of each domain as a whole: gRPC streams the full series of points, and MQTT publishes the first point per domain (`njord/lucerne/weather/state`). The setting is still bound and validated, but it does not create per-horizon topics or entities.

## Valid range

Each horizon must be between **1** and **96** hours, and at least one is required. Startup fails otherwise.

## Forecast length

How far ahead the data reaches is set by `ForecastDays` (default `4`, range 1 to 16) for the Weather domain. The Flood domain always requests a longer window (92 days), and Air Quality uses its own forecast range.

```json
{
  "Njord": {
    "ForecastDays": 7
  }
}
```

A longer `ForecastDays` raises the API call weight only when the request grows past Open-Meteo's fractional-weight thresholds. See [Budget](./budget).
