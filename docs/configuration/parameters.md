# Parameters

Parameters control which weather variables njord requests from the Open-Meteo API. Variables are organized into three groups that can be enabled independently.

## Configuration

```json
{
  "Njord": {
    "Parameters": {
      "Groups": ["Weather"],
      "Extra": [],
      "Exclude": []
    }
  }
}
```

The default is the `Weather` group only. Add `"Solar"` and/or `"Soil"` for additional variables.

## Parameter groups

### Weather (default)

**31 hourly variables:**

`temperature_2m`, `apparent_temperature`, `relative_humidity_2m`, `dew_point_2m`, `precipitation`, `rain`, `showers`, `snowfall`, `snow_depth`, `weather_code`, `cloud_cover`, `cloud_cover_low`, `cloud_cover_mid`, `cloud_cover_high`, `pressure_msl`, `surface_pressure`, `visibility`, `is_day`, `precipitation_probability`, `wind_speed_10m`, `wind_speed_80m`, `wind_speed_120m`, `wind_speed_180m`, `wind_direction_10m`, `wind_direction_80m`, `wind_direction_120m`, `wind_direction_180m`, `wind_gusts_10m`, `cape`, `freezing_level_height`, `vapour_pressure_deficit`

**17 daily variables:**

`temperature_2m_max`, `temperature_2m_min`, `apparent_temperature_max`, `apparent_temperature_min`, `weather_code`, `precipitation_sum`, `rain_sum`, `showers_sum`, `snowfall_sum`, `precipitation_hours`, `precipitation_probability_max`, `wind_speed_10m_max`, `wind_gusts_10m_max`, `wind_direction_10m_dominant`, `sunrise`, `sunset`, `daylight_duration`

### Solar

**9 hourly variables:**

`shortwave_radiation`, `direct_radiation`, `diffuse_radiation`, `direct_normal_irradiance`, `global_tilted_irradiance`, `terrestrial_radiation`, `sunshine_duration`, `uv_index`, `uv_index_clear_sky`

**4 daily variables:**

`shortwave_radiation_sum`, `sunshine_duration`, `uv_index_max`, `uv_index_clear_sky_max`

### Soil

**11 hourly variables:**

`soil_temperature_0cm`, `soil_temperature_6cm`, `soil_temperature_18cm`, `soil_temperature_54cm`, `soil_moisture_0_to_1cm`, `soil_moisture_1_to_3cm`, `soil_moisture_3_to_9cm`, `soil_moisture_9_to_27cm`, `soil_moisture_27_to_81cm`, `evapotranspiration`, `et0_fao_evapotranspiration`

**1 daily variable:**

`et0_fao_evapotranspiration`

## Extra and Exclude lists

Use `Extra` to add individual variables without enabling an entire group, or `Exclude` to remove specific variables from enabled groups:

```json
{
  "Njord": {
    "Parameters": {
      "Groups": ["Weather"],
      "Extra": ["uv_index", "uv_index_clear_sky"],
      "Exclude": [
        "wind_speed_80m", "wind_speed_120m", "wind_speed_180m",
        "wind_direction_80m", "wind_direction_120m", "wind_direction_180m"
      ]
    }
  }
}
```

This adds UV index from the Solar group while removing high-altitude wind variables you may not need.

## API call weight

Open-Meteo weights API requests based on the number of hourly variables:

```
weight = ceil(hourlyVariableCount / 10)
```

| Configuration | Hourly vars | Weight |
|---------------|-------------|--------|
| Weather only | 31 | 4 |
| Weather + Solar | 40 | 4 |
| Weather + Solar + Soil | 51 | 6 |
| Weather with excludes (example above) | 25 | 3 |

Higher weights count more against the [API budget](./budget). Use `Exclude` to reduce weight if you are approaching budget limits.
