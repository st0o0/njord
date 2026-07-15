# Horizons

Horizons define the forecast time offsets that njord publishes as separate sensor values. Each horizon produces a sensor per parameter per model.

## Configuration

```json
{
  "Njord": {
    "Horizons": [3, 6, 12, 24, 48, 72]
  }
}
```

The default is `[3, 6, 12, 24, 48, 72]` — forecasts for 3, 6, 12, 24, 48, and 72 hours ahead.

## What each horizon means

Each value represents how many hours into the future the forecast applies:

| Horizon | Topic segment | Meaning |
|---------|---------------|---------|
| `3` | `h3` | 3 hours from now |
| `6` | `h6` | 6 hours from now |
| `12` | `h12` | 12 hours from now |
| `24` | `h24` | Tomorrow, same time |
| `48` | `h48` | 2 days from now |
| `72` | `h72` | 3 days from now |
| `96` | `h96` | 4 days from now |

## Valid range

Each horizon must be between **1** and **96** (hours). At least one horizon is required.

## Model horizon limits

Not every model provides data out to every horizon. For example, `icon_d2` only forecasts ~48 hours ahead. When a model has no data at a configured horizon:

- njord does not publish a value for that model at that horizon
- No null payloads are sent — the sensor simply does not update
- The `expire_after` mechanism (2x poll interval) marks the sensor as unavailable in Home Assistant

This is expected behavior: short-range regional models naturally cover fewer horizons than global models.

## Fine-grained presets

For high-resolution monitoring of the next 24 hours, use closely spaced horizons:

```json
{
  "Njord": {
    "Horizons": [1, 2, 3, 4, 6, 8, 12, 18, 24, 36, 48, 72]
  }
}
```

::: warning
More horizons means more sensors per model device. With the default Weather parameter group and 12 horizons, each model device has over 100 sensors. Consider using [parameter excludes](./parameters) to reduce the sensor count, and see [budget](./budget) for API usage impact.
:::

## Sensor count formula

The number of sensors per model device is:

```
(hourly parameters x horizons) + (daily parameters x forecast days)
```

With defaults (31 hourly + 17 daily parameters, 6 horizons, 4 forecast days): `31 x 6 + 17 x 4 = 254` sensors per model device.
