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
    "Features": { "Flood": { "FloodRisk": true } },
    "Enrichment": { "Alerts": { ... } }
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
| `Features:<Category>:<Name>` | 14 weather alerts, `Lifestyle:Commute`, `AirQuality:Pollen`, `AirQuality:Pm25` on; all others off | Switches one feature on or off. The domains that are polled are derived from the enabled features (see below) |
| `Persistence:DataPath` | `data/njord.db` | SQLite database file |

## Defaults

All defaults are property initializers on the options classes. `appsettings.json` in the image only holds logging and Kestrel settings. [`appsettings.Example.json`](https://github.com/st0o0/njord/blob/main/src/Njord/appsettings.Example.json) is a template with deployment-specific keys only.

List-valued defaults (the alert threshold arrays) are built in: a list you configure replaces the built-in list completely, it is never merged by position. To go back to a default, remove your key.

## Features and domains

You switch features on, njord derives which API domains to poll. Each feature has a toggle under `Njord:Features:<Category>:<Name>` (environment variable `Njord__Features__<Category>__<Name>`); the lowercase name is its key on the wire; the polled domains are the union of the domains the enabled features read. A feature that is not listed keeps its default. The [Config Builder](/builder) shows the extra API calls per day each feature costs.

| Feature | Category | Default | Domains |
|---------|----------|---------|---------|
| `Weather:Frost` | Weather alerts | on | Weather |
| `Weather:Heat` | Weather alerts | on | Weather |
| `Weather:Storm` | Weather alerts | on | Weather |
| `Weather:HeavyRain` | Weather alerts | on | Weather |
| `Weather:Uv` | Weather alerts | on | Weather |
| `Weather:Fog` | Weather alerts | on | Weather |
| `Weather:Snow` | Weather alerts | on | Weather |
| `Weather:PressureDrop` | Weather alerts | on | Weather |
| `Weather:Thunderstorm` | Weather alerts | on | Weather |
| `Weather:Ice` | Weather alerts | on | Weather |
| `Weather:WindChill` | Weather alerts | on | Weather |
| `Weather:Visibility` | Weather alerts | on | Weather |
| `Weather:TropicalNight` | Weather alerts | on | Weather |
| `Weather:Humidity` | Weather alerts | on | Weather |
| `AirQuality:Pollen` | Air quality | on | Air Quality |
| `AirQuality:Pm25` | Air quality | on | Air Quality |
| `AirQuality:PollenTrend` | Air quality | off | Air Quality |
| `Lifestyle:Commute` | Lifestyle | on | Weather |
| `Lifestyle:Exercise` | Lifestyle | off | Weather, Air Quality |
| `Lifestyle:Laundry` | Lifestyle | off | Weather |
| `Lifestyle:Outdoor` | Lifestyle | off | Weather, Air Quality |
| `Lifestyle:Bbq` | Lifestyle | off | Weather |
| `Lifestyle:NightVentilation` | Lifestyle | off | Weather |
| `Lifestyle:Ventilation` | Lifestyle | off | Weather, Air Quality |
| `Lifestyle:Gardening` | Lifestyle | off | Weather |
| `Insight:ConfidenceTrend` | Insight | off | Weather |
| `Insight:FrontApproaching` | Insight | off | Weather |
| `Insight:SeasonalOutlook` | Insight | off | Seasonal |
| `Energy:SolarYield` | Solar | off | Weather |
| `Energy:SatelliteRadiation` | Solar | off | Satellite |
| `Flood:FloodRisk` | Flood | off | Flood |
| `Flood:FloodRain` | Flood | off | Weather, Flood |
| `Flood:FloodTimeline` | Flood | off | Flood |
| `Marine:Swimming` | Marine | off | Marine |
| `Marine:Surf` | Marine | off | Weather, Marine |

The defaults poll Weather and Air Quality (two calls per location and cycle). Enabling `Flood:FloodRisk` adds Flood; disabling `AirQuality:Pollen` and `AirQuality:Pm25` drops Air Quality. A disabled feature is not evaluated even when its domain data is present because another feature needs it, so each weather alert can be switched off on its own. `Insight:SeasonalOutlook` reads the Seasonal domain (monthly anomalies, `Njord:Seasonal:ForecastMonths`, default 3, max 6); it learns its own roughly daily update cycle like every domain and costs about one call per day and location. `Energy:SatelliteRadiation` reads the Satellite domain (observed GHI and tilted irradiance from `satellite-api.open-meteo.com`, tilt and azimuth from `Njord:SolarYield`, `Njord:Satellite:PastHours`, default 6, max 48); it learns its roughly hourly update cycle like every domain and costs about 720 calls per month and location. With `Energy:SolarYield` enabled as well, observed hours use the satellite irradiance and later hours the ensemble. At least one feature must be enabled. The former `Njord:Enrichments` section is no longer read.

```json
{ "Njord": { "Features": { "Flood": { "FloodRisk": true }, "AirQuality": { "Pollen": false }, "Weather": { "Heat": false } } } }
```

## Environment variable overrides

Use double-underscore notation; array items use zero-based indices:

```
Njord__Polling__PollInterval=00:30:00
Njord__Locations__0__Name=Lucerne
Njord__Locations__0__Latitude=47.05
Njord__Locations__0__Longitude=8.31
Njord__Weather__ForecastDays=4
Njord__Enrichment__Alerts__Enabled=true
Njord__Features__Flood__FloodRisk=true
```

::: tip
Use environment variables for secrets like `Njord__Persistence__ConnectionString`.
:::

## Configuration sections

- [Locations](./locations): where to fetch forecasts for
- [Weather](./weather): solar variables and forecast days
- [Enrichment](./enrichment): weather, air quality and flood alerts
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
