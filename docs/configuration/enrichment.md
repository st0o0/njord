# Enrichment

Enrichment features compute derived data from the raw model forecasts. Each feature is independently toggleable and publishes to its own Home Assistant device per location.

## Overview

| Feature | Default | Description |
|---------|---------|-------------|
| [Consensus](#consensus) | enabled | Cross-model aggregation with spread and agreement metrics |
| [Alerts](#alerts) | enabled | Weather alerts derived from model data |
| [Derived](#derived) | enabled | Beaufort scale, wind chill, comfort index, WMO descriptions |
| [Trends](#trends) | disabled | Temperature/wind/precipitation trend directions and timing |
| [Indices](#indices) | disabled | Activity scores, degree days, VPD |
| [Energy](#energy) | disabled | Heat pump COP, heating demand, solar/battery optimization |
| [History](#history) | disabled | Forecast accuracy tracking and model weighting |

## Consensus

Aggregates forecasts across all models for a location into a single consensus value per parameter and horizon.

```json
{
  "Enrichment": {
    "Consensus": {
      "Enabled": true,
      "Method": "Median",
      "TrimPercent": 0.1
    }
  }
}
```

| Option | Default | Description |
|--------|---------|-------------|
| `Method` | `"Median"` | Aggregation method: `"Median"` or `"TrimmedMean"` |
| `TrimPercent` | `0.1` | Fraction to trim from each end when using `TrimmedMean` |

Publishes to `njord/{location}/consensus/h{N}` with additional metadata: `_spread` (range), `_agreement` (ratio of models within threshold), `_models_used` (count), IQR, outlier detection, and confidence intervals.

## Alerts

Generates weather alerts as binary sensors with severity levels (None, Yellow, Orange, Red) based on configurable thresholds.

```json
{
  "Enrichment": {
    "Alerts": {
      "Enabled": true,
      "FrostThreshold": 0.0,
      "HeatThresholds": [30, 35, 40],
      "StormGustThreshold": 16.7,
      "HeavyRainHourlyThreshold": 10.0,
      "HeavyRainDailyThreshold": 25.0,
      "PressureDropThreshold": 5.0,
      "CapeThreshold": 1000.0,
      "ThunderstormPrecipThreshold": 5.0,
      "ThunderstormGustThreshold": 15.0
    }
  }
}
```

**Alert types:** Frost, Heat, Storm, HeavyRain, UV, Fog, Snow, PressureDrop, Thunderstorm

Each alert publishes as a `binary_sensor` with attributes:
- `severity` — None, Yellow, Orange, or Red
- `confidence` — how many models agree
- Type-specific fields (e.g. `expected_low`, `earliest_frost`, `models_agreeing`)

::: tip
Thresholds use metric units: temperatures in Celsius, wind speed in m/s, rain in mm, pressure in hPa, CAPE in J/kg.
:::

## Derived

Computes human-readable interpretations from raw forecast data.

```json
{
  "Enrichment": {
    "Derived": {
      "Enabled": true
    }
  }
}
```

**Per-horizon values:**
- `beaufort` — wind speed on the Beaufort scale (0--12)
- `wind_chill` — perceived temperature in Celsius
- `dewpoint_comfort` — comfort category based on dew point (e.g. "Comfortable", "Humid")
- `wmo_description` — human-readable weather description from WMO code

**Scalar values:**
- `diurnal_amplitude` — daily temperature range in Celsius
- `sunshine_pct` — percentage of daylight hours with sunshine
- `inversion` — temperature inversion detected (boolean)

## Trends

Analyzes how forecast parameters change over time.

```json
{
  "Enrichment": {
    "Trends": {
      "Enabled": false
    }
  }
}
```

Publishes to `njord/{location}/trends`:
- Parameter trend directions for temperature, wind, precipitation, cloud cover
- Weather change description (from WMO code transitions)
- Precipitation timing: starts-in / ends-in hours
- Temperature extrema timing: max-in / min-in hours
- Consensus stability label and ratio
- Predictability decay rate and reliable-hours estimate

## Indices

Activity-oriented scores and degree-day calculations.

```json
{
  "Enrichment": {
    "Indices": {
      "Enabled": false,
      "HeatingBaseTemp": 18.0,
      "CoolingBaseTemp": 24.0,
      "IndoorTemp": 22.0
    }
  }
}
```

| Option | Default | Description |
|--------|---------|-------------|
| `HeatingBaseTemp` | `18.0` | Base temperature for heating degree days (Celsius) |
| `CoolingBaseTemp` | `24.0` | Base temperature for cooling degree days (Celsius) |
| `IndoorTemp` | `22.0` | Assumed indoor temperature (Celsius) |

**Published values:**
- Activity scores: laundry, outdoor, running, cycling, BBQ, irrigation, solar, ventilation
- Degree days: HDD (heating), CDD (cooling)
- Frost: hours-until-frost, confidence
- VPD: vapour pressure deficit value (kPa) and category

## Energy

Heat pump and energy optimization calculations.

```json
{
  "Enrichment": {
    "Energy": {
      "Enabled": false,
      "FlowTemp": 35.0,
      "CarnotEfficiency": 0.45,
      "HeatingBaseTemp": 18.0,
      "CopOptimalHours": 3,
      "IndoorTemp": 22.0
    }
  }
}
```

| Option | Default | Description |
|--------|---------|-------------|
| `FlowTemp` | `35.0` | Heating system flow temperature (Celsius) |
| `CarnotEfficiency` | `0.45` | Fraction of Carnot COP the heat pump achieves |
| `HeatingBaseTemp` | `18.0` | Temperature below which heating is needed (Celsius) |
| `CopOptimalHours` | `3` | Number of optimal hours to identify for heating |
| `IndoorTemp` | `22.0` | Target indoor temperature (Celsius) |

**Published values:**
- Heat pump COP estimate and optimal heating hours
- Heating demand classification
- Shading recommendation
- Battery charge/discharge strategy
- Night cooling recommendation

## History

Tracks forecast accuracy over time by comparing past predictions against observations. Requires [persistence](./persistence) to be configured.

```json
{
  "Enrichment": {
    "History": {
      "Enabled": false,
      "RetentionDays": 30,
      "MinSampleSize": 48,
      "SnapshotInterval": 100
    }
  }
}
```

| Option | Default | Description |
|--------|---------|-------------|
| `RetentionDays` | `30` | How many days of history to keep |
| `MinSampleSize` | `48` | Minimum data points before publishing accuracy metrics |
| `SnapshotInterval` | `100` | Events between persistence snapshots |

**Published values:**
- Per-model: MAE (7-day and 30-day), weight, drift
- Seasonal best model
- Anomaly detection (boolean + deviation in sigma)
- Accuracy-weighted temperature

::: warning
History enrichment depends on Akka.Persistence. Make sure a [persistence provider](./persistence) is configured and the data volume is mounted.
:::
