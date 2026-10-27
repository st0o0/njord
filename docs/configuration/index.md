# Configuration

njord is configured through `appsettings.json` and environment variables. All settings live under the `Njord` section.

Configuration is **static**: it is read once at startup. There is no runtime configuration API and no override file. To change a setting, edit your configuration or environment and restart the container; a restart takes seconds.

## Structure overview

```json
{
  "Njord": {
    "Locations": [ ... ],
    "Weather": { "ForecastDays": 4 },
    "Polling": {
      "PollInterval": "01:00:00",
      "DiscoveryInterval": "00:20:00",
      "BudgetOverride": null
    },
    "Persistence": { "DataPath": "data/njord.db" },
    "Features": { "Flood": { "FloodRisk": { "Enabled": true } } }
  }
}
```

A minimal setup for Lucerne:

```json
{
  "Njord": {
    "Locations": [{ "Name": "Lucerne", "Latitude": 47.05, "Longitude": 8.31 }]
  }
}
```

| Setting | Default | Description |
|---------|---------|-------------|
| `Polling:PollInterval` | `01:00:00` | How often each PollWorker fetches all domains of its location |
| `Polling:DiscoveryInterval` | `00:20:00` | Poll interval until a worker has learned the update cycle of the data |
| `Weather:ForecastDays` | `4` | Days of weather forecast to request (1 to 16) |
| `AirQuality:ForecastDays` | `5` | Days of air quality forecast to request (1 to 7) |
| `Marine:ForecastDays` | `8` | Days of marine forecast to request (1 to 8) |
| `Flood:ForecastDays` | `92` | Days of river discharge forecast to request (1 to 210) |
| `Features:<Category>:<Name>:Enabled` | 18 features on by default (14 original weather alerts + RainProbability + Commute + Pollen + PM2.5); all others off | Switches one feature on or off. The domains that are polled are derived from the enabled features (see below) |
| `Persistence:DataPath` | `data/njord.db` | SQLite database file |

## Defaults

All defaults are property initializers on the options classes. `appsettings.json` in the image only holds logging and Kestrel settings. [`appsettings.Example.json`](https://github.com/st0o0/njord/blob/main/src/Njord/appsettings.Example.json) is a template with deployment-specific keys only.

List-valued defaults (the feature threshold arrays) are built in: a list you configure replaces the built-in list completely, it is never merged by position. To go back to a default, remove your key.

## Features and domains

You switch features on, njord derives which API domains to poll. Each feature has its own section `Njord:Features:<Category>:<Name>` with an `Enabled` toggle (environment variable `Njord__Features__<Category>__<Name>__Enabled`) and, where it has any, its options such as `Thresholds` (see [Enrichment](./enrichment)); the polled domains are the union of the domains the enabled features read. A feature that is not listed keeps its default. The [Config Builder](/builder) shows the extra API calls per day each feature costs.

### Weather alerts (20)

| Feature | Default | Domains | What it does |
|---------|---------|---------|-------------|
| `Weather:Frost` | on | Weather, Ensemble | Warns when temperature drops below freezing. Thresholds: 0 / -5 / -15 °C. |
| `Weather:Heat` | on | Weather, Ensemble | Warns when air or apparent temperature exceeds heat thresholds. Thresholds: 30 / 35 / 40 °C. |
| `Weather:Storm` | on | Weather, Ensemble | Warns when wind gusts exceed storm thresholds. Thresholds: 17 / 25 / 33 m/s. |
| `Weather:HeavyRain` | on | Weather, Ensemble | Warns when hourly or daily precipitation is heavy. Defaults: 10 mm/h, 25 mm/day. |
| `Weather:Uv` | on | Weather | Warns when the UV index is high. Uses the deterministic UV forecast. |
| `Weather:Fog` | on | Weather | Warns when conditions favor fog (dew point near temperature, low wind, high humidity). |
| `Weather:Snow` | on | Weather, Ensemble | Warns when snowfall exceeds thresholds and the freezing level is low. |
| `Weather:PressureDrop` | on | Weather | Warns when atmospheric pressure drops rapidly (5 / 10 hPa in 3 hours). |
| `Weather:Thunderstorm` | on | Weather, Ensemble | Warns when CAPE, precipitation and gusts indicate thunderstorm risk. Enhanced with CIN and lifted index. |
| `Weather:Ice` | on | Weather, Ensemble | Warns when rain meets freezing temperatures, creating black ice conditions. |
| `Weather:WindChill` | on | Weather, Ensemble | Warns when wind chill (apparent temperature) drops to dangerous levels. Thresholds: -10 / -20 / -30 °C. |
| `Weather:Visibility` | on | Weather | Warns when visibility drops below thresholds. Thresholds: 1000 / 200 / 50 m. |
| `Weather:TropicalNight` | on | Weather, Ensemble | Warns when the nighttime low stays above 20 °C. Thresholds: 20 / 23 / 25 °C. |
| `Weather:Humidity` | on | Weather | Warns when the daytime dew point indicates oppressive humidity. Thresholds: 16 / 21 / 24 °C. |
| `Weather:RainProbability` | on | Weather, Ensemble | Warns when rain probability is high in a rolling window. Thresholds: 50 / 70 / 90 %. Window: 6 hours. |
| `Weather:WetBulbDanger` | off | Weather, Ensemble | Warns when wet-bulb temperature indicates heat stroke risk. Thresholds: 26 / 28 / 32 °C. Medically critical. |
| `Weather:Shower` | off | Weather, Ensemble | Warns when convective showers are expected. Thresholds: 3 / 5 / 10 mm/h. |
| `Weather:SnowDepthWatch` | off | Weather | Reports current snow depth status: light (>2 cm), moderate (>10 cm), heavy (>25 cm), extreme (>50 cm). |
| `Lifestyle:ClearSkyUv` | off | Weather | Warns when clear-sky UV is high -- the UV you'd get if clouds part. Thresholds: 6 / 8 / 10 UV index. |
| `Lifestyle:FireRisk` | off | Weather, Ensemble | Wildfire risk index from temperature, humidity, wind, soil moisture and dry spell duration. |

### Air quality alerts and sensors (7)

| Feature | Default | Domains | What it does |
|---------|---------|---------|-------------|
| `AirQuality:Pollen` | on | Air Quality | Warns when pollen concentration is high (birch, grass, alder, mugwort). |
| `AirQuality:Pm25` | on | Air Quality | Warns when PM2.5 exceeds WHO guidelines (15 / 25 / 50 ug/m3). |
| `AirQuality:Dust` | off | Air Quality | Warns when Saharan dust concentration is high. Thresholds: 50 / 150 / 500 ug/m3. |
| `AirQuality:PollenTrend` | off | Air Quality | Shows the trend of pollen load over recent polls. |
| `AirQuality:SmogRisk` | off | Weather, Air Quality | Warns when a low atmospheric boundary layer combines with high PM2.5, indicating smog. |
| `AirQuality:PollenBlast` | off | Weather, Air Quality | Predicts pollen explosion events when warm, dry, windy weather follows a rain period. |
| `AirQuality:RagweedPollen` | off | Air Quality | Ragweed pollen concentration sensor (relevant in North America and parts of Europe). |
| `AirQuality:GranularAqi` | off | Air Quality | Per-pollutant European AQI sub-indices (PM2.5, PM10, NO2, ozone, SO2). |

### Flood alerts (3)

| Feature | Default | Domains | What it does |
|---------|---------|---------|-------------|
| `Flood:FloodRisk` | off | Flood | Warns when river discharge exceeds historical thresholds (median, P75, max). |
| `Flood:FloodRain` | off | Weather, Flood | Cross-domain: warns when heavy forecast rain meets rising river discharge. |
| `Flood:FloodTimeline` | off | Flood | Shows the discharge timeline for the coming days. |

### Marine alerts and conditions (4)

| Feature | Default | Domains | What it does |
|---------|---------|---------|-------------|
| `Marine:Swimming` | off | Marine | Warns when wave height or water temperature make swimming unsafe. |
| `Marine:Surf` | off | Weather, Marine | Rates surf conditions from swell height, period and wind direction. |
| `Marine:OceanCurrents` | off | Marine | Reports ocean current speed and direction for maritime activities. |
| `Marine:CoastalFlood` | off | Marine, Weather, Ensemble | Storm surge risk from sea level deviation, wave height and onshore wind. |

### Lifestyle scores and conditions (11)

| Feature | Default | Domains | What it does |
|---------|---------|---------|-------------|
| `Lifestyle:Commute` | on | Weather | Rates commute safety from rain, wind, ice risk and visibility. |
| `Lifestyle:Exercise` | off | Weather, Air Quality | Exercise advisory considering heat, humidity, UV and air quality. |
| `Lifestyle:Laundry` | off | Weather | Rates outdoor drying conditions from temperature, wind, rain probability and sunshine. |
| `Lifestyle:Outdoor` | off | Weather, Air Quality | Overall outdoor activity score combining weather and air quality. |
| `Lifestyle:Bbq` | off | Weather | Barbecue suitability score from temperature, wind and rain. |
| `Lifestyle:NightVentilation` | off | Weather | Advises when nighttime temperatures are cool enough for passive cooling. |
| `Lifestyle:Ventilation` | off | Weather, Air Quality | Finds the best window for airing rooms, considering temperature, rain, pollen and PM2.5. |
| `Lifestyle:Gardening` | off | Weather | Gardening advisory from temperature, wind and precipitation. |
| `Lifestyle:SoilMoisture` | off | Weather | Soil moisture status: dry (needs watering), optimal, or wet (stay off the lawn). |
| `Lifestyle:Evapotranspiration` | off | Weather | Daily water loss in mm for irrigation guidance. |
| `Lifestyle:GrowingDegree` | off | Weather | Growing Degree Days accumulation for agricultural planning (configurable base temperature). |

### Insight, energy and model comparison (7)

| Feature | Default | Domains | What it does |
|---------|---------|---------|-------------|
| `Insight:ConfidenceTrend` | off | Ensemble | Tracks how the ensemble forecast confidence changes over time. |
| `Insight:FrontApproaching` | off | Weather | Early warning when a pressure front approaches (pressure + temperature + wind shift). |
| `Insight:SeasonalOutlook` | off | Seasonal | Monthly temperature and precipitation anomaly outlook for the next 3 months. |
| `Weather:ModelComparison` | off | Ensemble | Side-by-side daily temperature and precipitation from each configured ensemble model. |
| `Energy:SolarYield` | off | Weather | Expected solar energy yield from tilted panel irradiance. |
| `Energy:SatelliteRadiation` | off | Satellite | Satellite-observed solar irradiance blended with the forecast. |
| `Energy:ElevatedWind` | off | Weather | 80m and 120m hub-height wind speed and direction for wind energy monitoring. |

### How domains are derived

The defaults (18 features on) poll Weather, Ensemble and Air Quality. Enabling a Flood feature adds Flood; enabling a Marine feature adds Marine. Disabling all Air Quality features drops Air Quality from polling. Each domain polls independently on its own learned cycle.

A disabled feature is not evaluated even when its domain data is present because another feature needs it, so each alert can be switched off individually. At least one feature must be enabled.

```json
{ "Njord": { "Features": { "Flood": { "FloodRisk": { "Enabled": true } }, "AirQuality": { "Pollen": { "Enabled": false } }, "Weather": { "Heat": { "Enabled": false } } } } }
```

## Environment variable overrides

Use double-underscore notation; array items use zero-based indices:

```
Njord__Polling__PollInterval=00:30:00
Njord__Locations__0__Name=Lucerne
Njord__Locations__0__Latitude=47.05
Njord__Locations__0__Longitude=8.31
Njord__Weather__ForecastDays=4
Njord__Features__Flood__FloodRisk__Enabled=true
Njord__Features__Weather__Frost__Thresholds__0=1
```

::: tip
Use environment variables for secrets like `Njord__Persistence__ConnectionString`.
:::

## Configuration sections

- [Locations](./locations): where to fetch forecasts for
- [Weather](./weather): solar variables and forecast days
- [Enrichment](./enrichment): feature results and alert thresholds
- [Persistence](./persistence): SQLite or PostgreSQL storage
- [Budget](./budget): Open-Meteo limits and usage projection

## Logging

Log levels live in the `Serilog` section, outside `Njord`. Every log line shows the full type name of the class that wrote it (`[Njord.Polling.PollWorker]`); Akka.NET internals log as `Akka.*`. An override key matches its category and everything below it:

```json
{
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Akka": "Warning",
        "Njord.Polling": "Debug",
        "Njord.Pipeline": "Debug"
      }
    }
  }
}
```

`Njord.Polling` shows the Debug logs of the poll manager and the poll workers, `Njord.Pipeline` those of the analysis pipeline. As environment variable: `Serilog__MinimumLevel__Override__Njord.Pipeline=Debug`. Log levels are read at startup.

## Startup validation

njord validates the configuration at startup and refuses to start if:

- No locations are defined
- A location has an empty name
- `Weather:ForecastDays` is outside 1 to 16
- `AirQuality:ForecastDays` is outside 1 to 7, `Marine:ForecastDays` outside 1 to 8, or `Flood:ForecastDays` outside 1 to 210
- PostgreSQL is selected without a connection string
- Projected API usage exceeds 80% of the monthly budget
