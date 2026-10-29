# Configuration

njord is configured through `appsettings.json` and environment variables. All settings live under the `Njord` section.

Configuration is **static**: it is read once at startup. There is no runtime configuration API and no override file. To change a setting, edit your configuration or environment and restart the container; a restart takes seconds.

## Structure overview

```json
{
  "Njord": {
    "Locations": [ ... ],
    "Forecast": { "ForecastDays": 4 },
    "Polling": {
      "PollInterval": "01:00:00",
      "DiscoveryInterval": "00:20:00",
      "BudgetOverride": null
    },
    "Persistence": { "DataPath": "data/njord.db" },
    "Features": { "FloodRisk": { "Enabled": true } }
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
| `Forecast:ForecastDays` | `4` | Days of weather forecast to request (1 to 16) |
| `AirQuality:ForecastDays` | `5` | Days of air quality forecast to request (1 to 7) |
| `Marine:ForecastDays` | `8` | Days of marine forecast to request (1 to 8) |
| `Flood:ForecastDays` | `92` | Days of river discharge forecast to request (1 to 210) |
| `Features:<Name>:Enabled` | 18 features on by default (14 original weather alerts + RainProbability + Commute + Pollen + PM2.5); all others off | Switches one feature on or off. The domains that are polled are derived from the enabled features (see below) |
| `Persistence:DataPath` | `data/njord.db` | SQLite database file |

## Defaults

All defaults are property initializers on the options classes. `appsettings.json` in the image only holds logging and Kestrel settings. [`appsettings.Example.json`](https://github.com/st0o0/njord/blob/main/src/Njord/appsettings.Example.json) is a template with deployment-specific keys only.

List-valued defaults (the feature threshold arrays) are built in: a list you configure replaces the built-in list completely, it is never merged by position. To go back to a default, remove your key.

## Features and domains

You switch features on, njord derives which API domains to poll. Each feature has its own section `Njord:Features:<Name>` with an `Enabled` toggle (environment variable `Njord__Features__<Name>__Enabled`) and, where it has any, its options such as `Thresholds` (see [Enrichment](./enrichment)); the polled domains are the union of the domains the enabled features read. A feature that is not listed keeps its default. The [Config Builder](/builder) shows the extra API calls per day each feature costs.

See [Features](/features/) for the complete list of all 65 features, their domains and defaults.

### How domains are derived

The defaults (18 features on) poll Forecast, Ensemble and Air Quality. Enabling a Flood feature adds Flood; enabling a Marine feature adds Marine. Disabling all Air Quality features drops Air Quality from polling. Each domain polls independently on its own learned cycle.

A disabled feature is not evaluated even when its domain data is present because another feature needs it, so each alert can be switched off individually. At least one feature must be enabled.

```json
{ "Njord": { "Features": { "FloodRisk": { "Enabled": true }, "Pollen": { "Enabled": false }, "Heat": { "Enabled": false } } } }
```

## Environment variable overrides

Use double-underscore notation; array items use zero-based indices:

```
Njord__Polling__PollInterval=00:30:00
Njord__Locations__0__Name=Lucerne
Njord__Locations__0__Latitude=47.05
Njord__Locations__0__Longitude=8.31
Njord__Forecast__ForecastDays=4
Njord__Features__FloodRisk__Enabled=true
Njord__Features__Frost__Thresholds__0=1
```

::: tip
Use environment variables for secrets like `Njord__Persistence__ConnectionString`.
:::

## Configuration sections

- [Locations](./locations): where to fetch forecasts for
- [Forecast and Ensemble](./weather): forecast days and ensemble models
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
- `Forecast:ForecastDays` is outside 1 to 16
- `AirQuality:ForecastDays` is outside 1 to 7, `Marine:ForecastDays` outside 1 to 8, or `Flood:ForecastDays` outside 1 to 210
- PostgreSQL is selected without a connection string
- Projected API usage exceeds 80% of the monthly budget
