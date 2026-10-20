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
    "Features": { "FloodRisk": true },
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
| `Features:<Name>` | 14 weather alerts, `CommuteSafety`, `PollenAlert`, `Pm25Alert` on; all others off | Switches one feature on or off. The domains that are polled are derived from the enabled features (see below) |
| `Persistence:DataPath` | `data/njord.db` | SQLite database file |

## Defaults

All defaults are property initializers on the options classes. `appsettings.json` in the image only holds logging and Kestrel settings. [`appsettings.Example.json`](https://github.com/st0o0/njord/blob/main/src/Njord/appsettings.Example.json) is a template with deployment-specific keys only.

List-valued defaults (the alert threshold arrays) are built in: a list you configure replaces the built-in list completely, it is never merged by position. To go back to a default, remove your key.

## Features and domains

You switch features on, njord derives which API domains to poll. Each feature has a toggle under `Njord:Features`; the polled domains are the union of the domains the enabled features read. A feature that is not listed keeps its default. The [Config Builder](/builder) shows the extra API calls per day each feature costs.

| Feature | Category | Default | Domains |
|---------|----------|---------|---------|
| `FrostAlert` | Weather alerts | on | Weather |
| `HeatAlert` | Weather alerts | on | Weather |
| `StormAlert` | Weather alerts | on | Weather |
| `RainAlert` | Weather alerts | on | Weather |
| `UvAlert` | Weather alerts | on | Weather |
| `FogAlert` | Weather alerts | on | Weather |
| `SnowAlert` | Weather alerts | on | Weather |
| `PressureDropAlert` | Weather alerts | on | Weather |
| `ThunderstormAlert` | Weather alerts | on | Weather |
| `IceAlert` | Weather alerts | on | Weather |
| `WindChillAlert` | Weather alerts | on | Weather |
| `VisibilityAlert` | Weather alerts | on | Weather |
| `TropicalNightAlert` | Weather alerts | on | Weather |
| `HumidityAlert` | Weather alerts | on | Weather |
| `PollenAlert` | Air quality | on | Air Quality |
| `Pm25Alert` | Air quality | on | Air Quality |
| `PollenTrend` | Air quality | off | Air Quality |
| `CommuteSafety` | Lifestyle | on | Weather |
| `ExerciseAdvisory` | Lifestyle | off | Weather, Air Quality |
| `LaundryScore` | Lifestyle | off | Weather |
| `OutdoorScore` | Lifestyle | off | Weather, Air Quality |
| `BBQScore` | Lifestyle | off | Weather |
| `NightVentilation` | Lifestyle | off | Weather |
| `VentilationWindow` | Lifestyle | off | Weather, Air Quality |
| `GardeningAdvisory` | Lifestyle | off | Weather |
| `ConfidenceTrend` | Insight | off | Weather |
| `ApproachingFront` | Insight | off | Weather |
| `SeasonalOutlook` | Insight | off | Seasonal |
| `SolarYield` | Solar | off | Weather |
| `FloodRisk` | Flood | off | Flood |
| `FloodRain` | Flood | off | Weather, Flood |
| `FloodTimeline` | Flood | off | Flood |
| `SwimmingConditions` | Marine | off | Marine |
| `SurfConditions` | Marine | off | Weather, Marine |

The defaults poll Weather and Air Quality (two calls per location and cycle). Enabling `FloodRisk` adds Flood; disabling `PollenAlert` and `Pm25Alert` drops Air Quality. A disabled feature is not evaluated even when its domain data is present because another feature needs it, so each weather alert can be switched off on its own. `SeasonalOutlook` reads the Seasonal domain (monthly anomalies, `Njord:Seasonal:ForecastMonths`, default 3, max 6); it learns its own roughly daily update cycle like every domain and costs about one call per day and location. At least one feature must be enabled. The former `Njord:Enrichments` section is no longer read.

```json
{ "Njord": { "Features": { "FloodRisk": true, "PollenAlert": false, "HeatAlert": false } } }
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
Njord__Features__FloodRisk=true
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
