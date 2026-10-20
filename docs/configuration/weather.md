# Weather

Options for the Weather domain under `Njord:Weather`. The model is fixed: njord uses the Open-Meteo Ensemble Mean, and the variable list is built into the client. Air Quality, Flood and Marine use fixed variable sets too.

```json
{
  "Njord": {
    "Weather": {
      "IncludeSolar": true,
      "ForecastDays": 4
    }
  }
}
```

As environment variables: `Njord__Weather__IncludeSolar=true`, `Njord__Weather__ForecastDays=4`.

| Option | Default | Description |
|--------|---------|-------------|
| `IncludeSolar` | `false` | Adds `shortwave_radiation`, `direct_radiation`, `diffuse_radiation`, `direct_normal_irradiance` and `sunshine_duration` to the weather request |
| `ForecastDays` | `4` | Days of forecast to request (1 to 16) |

## API call weight

The weather request asks for 31 standard hourly variables, 36 with `IncludeSolar`. Open-Meteo counts a request as `ceil(variables / 10)` calls:

| Setting | Hourly variables | Call weight |
|---------|------------------|-------------|
| default | 31 | 3 |
| `IncludeSolar: true` | 36 | 4 |

The weight multiplies the weather calls in the [budget](./budget) projection. Solar is part of the same request, so it adds weight, not a separate call.

## Variables

`temperature_2m`, `apparent_temperature`, `relative_humidity_2m`, `dew_point_2m`, `precipitation`, `rain`, `showers`, `snowfall`, `snow_depth`, `weather_code`, `cloud_cover`, `cloud_cover_low`, `cloud_cover_mid`, `cloud_cover_high`, `pressure_msl`, `surface_pressure`, `visibility`, `is_day`, `precipitation_probability`, `wind_speed_10m`, `wind_speed_80m`, `wind_speed_120m`, `wind_speed_180m`, `wind_direction_10m`, `wind_direction_80m`, `wind_direction_120m`, `wind_direction_180m`, `wind_gusts_10m`, `cape`, `freezing_level_height`, `vapour_pressure_deficit`

With `IncludeSolar`: `shortwave_radiation`, `direct_radiation`, `diffuse_radiation`, `direct_normal_irradiance`, `sunshine_duration`.
