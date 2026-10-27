# Features

njord evaluates 53 features across seven categories. Each feature polls one or more weather domains, evaluates a rule against the forecast data, and delivers the result to Home Assistant as an entity.

Features are toggled with a single environment variable:

```
Njord__Features__<Category>__<Name>__Enabled=true
```

Use the [Config Builder](/builder) to pick features and preview the API budget.

## Categories at a Glance

| Category | What it covers | Features |
|----------|---------------|----------|
| [Weather Alerts](./weather-alerts) | Dangerous weather conditions | 20 alerts |
| [Air Quality](./air-quality) | Pollen, particulates, smog | 7 features |
| [Lifestyle](./lifestyle) | Daily life decisions | 12 features |
| [Energy](./energy) | Solar and wind energy | 3 features |
| [Flood](./flood) | River discharge and flood risk | 3 features |
| [Marine](./marine) | Waves, currents, coastal hazards | 3 features |
| [Insights](./insights) | Trends, forecasts, model comparison | 5 features |

## Feature Reference

Every feature creates one or more Home Assistant entities. The table below shows what each feature does, whether it is on by default, and which Open-Meteo domains it needs.

### Weather Alerts

| Feature | Default | Description | Domains |
|---------|---------|-------------|---------|
| Frost | On | Warns when temperature drops below 0 °C | Weather, Ensemble |
| Heat | On | Warns when temperature exceeds 30 °C | Weather, Ensemble |
| Storm | On | Warns when wind gusts exceed 17 m/s | Weather, Ensemble |
| HeavyRain | On | Warns when hourly rain exceeds 10 mm | Weather, Ensemble |
| UV | On | Warns when UV index exceeds safe levels | Weather |
| Fog | On | Warns when fog conditions form | Weather |
| Snow | On | Warns when significant snowfall is expected | Weather, Ensemble |
| PressureDrop | On | Warns when pressure drops rapidly | Weather |
| Thunderstorm | On | Warns when convective storms are likely | Weather, Ensemble |
| Ice | On | Warns when black ice conditions exist | Weather, Ensemble |
| WindChill | On | Warns when wind chill is dangerous | Weather, Ensemble |
| Visibility | On | Warns when visibility drops below 1 km | Weather |
| TropicalNight | On | Warns when night temperature stays above 20 °C | Weather, Ensemble |
| Humidity | On | Warns when muggy conditions occur | Weather |
| RainProbability | On | Warns when rain probability exceeds 50% | Weather, Ensemble |
| WetBulbDanger | Off | Warns when wet-bulb temperature indicates heat stroke risk | Weather, Ensemble |
| ClearSkyUv | Off | Warns about UV danger even under intermittent clouds | Weather |
| Shower | Off | Warns about sudden convective showers | Weather, Ensemble |
| FireRisk | Off | Warns about wildfire risk conditions | Weather, Ensemble |
| CoastalFlood | Off | Warns about storm surge risk | Marine, Weather, Ensemble |

### Air Quality

| Feature | Default | Description | Domains |
|---------|---------|-------------|---------|
| Pollen | On | Warns at high pollen concentrations | AirQuality |
| Pm25 | On | Warns when PM2.5 exceeds WHO guidelines | AirQuality |
| Dust | Off | Warns about Saharan dust events | AirQuality |
| PollenTrend | Off | Tracks pollen load trend over time | AirQuality |
| SmogRisk | Off | Warns when low mixing layer traps pollution | Weather, AirQuality |
| RagweedPollen | Off | Ragweed pollen concentration sensor | AirQuality |
| GranularAqi | Off | Per-pollutant European AQI sub-indices | AirQuality |

### Lifestyle

| Feature | Default | Description | Domains |
|---------|---------|-------------|---------|
| Commute | On | Rates commute safety conditions | Weather |
| Exercise | Off | Advises on outdoor exercise conditions | Weather, AirQuality |
| Laundry | Off | Scores outdoor laundry drying conditions | Weather |
| Outdoor | Off | Scores general outdoor activity suitability | Weather, AirQuality |
| Bbq | Off | Scores barbecue weather | Weather |
| NightVentilation | Off | Advises when to cool your home overnight | Weather |
| Ventilation | Off | Finds the best time to open windows | Weather, AirQuality |
| Gardening | Off | Advises on gardening conditions | Weather |
| SoilMoisture | Off | Reports topsoil moisture status | Weather |
| Evapotranspiration | Off | Guides daily irrigation needs | Weather |
| SnowDepthWatch | Off | Reports snow accumulation depth | Weather |
| ElevatedWind | Off | Reports hub-height wind for wind energy | Weather |

### Energy

| Feature | Default | Description | Domains |
|---------|---------|-------------|---------|
| SolarYield | Off | Forecasts solar panel energy output | Weather |
| SatelliteRadiation | Off | Blends satellite observations with forecast | Satellite |
| ElevatedWind | Off | 80 m and 120 m wind data for wind turbines | Weather |

### Flood

| Feature | Default | Description | Domains |
|---------|---------|-------------|---------|
| FloodRisk | Off | Warns when river discharge exceeds historical levels | Flood |
| FloodRain | Off | Warns when heavy rain meets rising discharge | Weather, Flood |
| FloodTimeline | Off | Projects discharge levels for coming days | Flood |

### Marine

| Feature | Default | Description | Domains |
|---------|---------|-------------|---------|
| Swimming | Off | Rates swimming safety from waves and water temperature | Marine |
| Surf | Off | Reports surf conditions from swell and wind | Weather, Marine |
| OceanCurrents | Off | Reports ocean current speed and direction | Marine |

### Insights

| Feature | Default | Description | Domains |
|---------|---------|-------------|---------|
| ConfidenceTrend | Off | Tracks how forecast confidence changes over time | Ensemble |
| ModelComparison | Off | Compares multiple weather models side by side | Ensemble |
| SeasonalOutlook | Off | Monthly temperature and precipitation outlook | Seasonal |
| FrontApproaching | Off | Detects approaching weather fronts | Weather |
| GrowingDegree | Off | Accumulates growing degree days for farming | Weather |

## How Features Work

Every feature is a **rule** that:

1. **Declares** which weather domains it reads (Weather, Ensemble, AirQuality, Flood, Marine, Seasonal, Satellite)
2. **Activates** those domains automatically -- you never configure which APIs to poll
3. **Evaluates** when its domain delivers new data
4. **Emits** a result that becomes a Home Assistant entity

::: tip Budget is feature-driven
Enabling a feature that needs the Flood domain automatically starts polling the Flood API. Disabling all Flood features stops it. You only pay for what you use.
:::

## Confidence Levels

Weather alerts that use the Ensemble domain receive a confidence rating based on how well weather models agree:

| Confidence | Meaning | Ensemble Spread |
|------------|---------|-----------------|
| High | Models agree closely | < 1 °C |
| Medium | Some disagreement | 1 -- 3 °C |
| Low | Models disagree significantly | > 3 °C |

Alerts that do not use the Ensemble domain always report Medium confidence. This keeps them fast and lightweight while still delivering useful warnings.
