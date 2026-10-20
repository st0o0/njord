# Configuration

njord is configured through `appsettings.json` and environment variables. All settings live under the `Njord` section.

Configuration is **static**: it is read once at startup. There is no runtime configuration API and no override file. To change a setting, edit your configuration or environment and restart the container; a restart takes seconds.

## Structure overview

```json
{
  "Njord": {
    "Locations": [ ... ],
    "Weather": { "IncludeSolar": false, "ForecastDays": 4 },
    "Polling": {
      "PollInterval": "01:00:00",
      "DiscoveryInterval": "00:20:00",
      "BudgetOverride": null
    },
    "OpenMeteo": { "BaseUrl": "https://api.open-meteo.com/" },
    "Persistence": { "DataPath": "data/njord.db" },
    "Mqtt": { ... },
    "Enrichments": { "FloodRisk": true },
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
| `Weather:IncludeSolar` | `false` | Adds solar radiation variables to the weather request (see [Weather](./weather)) |
| `Enrichments:<Name>` | `WeatherAlerts`, `CommuteSafety`, `PollenAlert`, `Pm25Alert` on; all others off | Switches one enrichment on or off. The domains that are polled are derived from the enabled enrichments (see below) |
| `OpenMeteo:BaseUrl` | `https://api.open-meteo.com/` | Open-Meteo endpoint for the weather domain (self-hosted instance or test double) |
| `Persistence:DataPath` | `data/njord.db` | SQLite database file |

## Defaults

All defaults are property initializers on the options classes. `appsettings.json` in the image only holds logging and Kestrel settings. [`appsettings.Example.json`](https://github.com/st0o0/njord/blob/main/src/Njord/appsettings.Example.json) is a template with deployment-specific keys only.

List-valued defaults (the alert threshold arrays) are built in: a list you configure replaces the built-in list completely, it is never merged by position. To go back to a default, remove your key.

## Enrichments and domains

You switch features on, njord derives which API domains to poll. Each enrichment has a toggle under `Njord:Enrichments`; the polled domains are the union of the domains the enabled enrichments read. `Njord:EnabledDomains` no longer exists and is ignored if still configured.

| Enrichment | Default | Domains |
|------------|---------|---------|
| `WeatherAlerts` | on | Weather |
| `CommuteSafety` | on | Weather |
| `PollenAlert` | on | Air Quality |
| `Pm25Alert` | on | Air Quality |
| `FloodRisk` | off | Flood |
| `FloodRain` | off | Flood, Weather |
| `SwimmingConditions` | off | Marine |
| `VentilationWindow` | off | Weather, Air Quality |
| `ExerciseAdvisory` | off | Weather, Air Quality |
| `LaundryScore` | off | Weather |
| `PollenTrend` | off | Air Quality |
| `NightVentilation` | off | Weather |
| `ConfidenceTrend` | off | Weather |

The defaults poll Weather and Air Quality (two calls per location and cycle). Enabling `FloodRisk` adds Flood; disabling `PollenAlert` and `Pm25Alert` drops Air Quality. A disabled enrichment is not evaluated even when its domain data is present because another enrichment needs it. At least one enrichment must be enabled.

```json
{ "Njord": { "Enrichments": { "FloodRisk": true, "PollenAlert": false } } }
```

## Environment variable overrides

Use double-underscore notation; array items use zero-based indices:

```
Njord__Polling__PollInterval=00:30:00
Njord__Locations__0__Name=Lucerne
Njord__Locations__0__Latitude=47.05
Njord__Locations__0__Longitude=8.31
Njord__Weather__IncludeSolar=true
Njord__Weather__ForecastDays=4
Njord__Enrichment__Alerts__Enabled=true
Njord__Enrichments__FloodRisk=true
Njord__Mqtt__Host=192.168.1.100
```

::: tip
Use environment variables for secrets like `Njord__Mqtt__Password`.
:::

## Configuration sections

- [Locations](./locations): where to fetch forecasts for
- [Weather](./weather): solar variables and forecast days
- [Enrichment](./enrichment): weather, air quality and flood alerts
- [MQTT](./mqtt): broker connection and topics
- [Persistence](./persistence): SQLite or PostgreSQL storage
- [Budget](./budget): Open-Meteo limits and usage projection

## Logging

Log levels live in the `Serilog` section, outside `Njord`. Every log line shows the full type name of the class that wrote it (`[Njord.Pipeline.PollWorker]`); Akka.NET internals log as `Akka.*`. An override key matches its category and everything below it:

```json
{
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Akka": "Warning",
        "Njord.Pipeline": "Debug",
        "Njord.Mqtt.MqttStatePublisher": "Debug"
      }
    }
  }
}
```

`Njord.Pipeline` shows the Debug logs of the poll manager, the poll workers and the analysis pipeline; `Njord.Mqtt.MqttStatePublisher` adds element-level tracing of the MQTT state stream. As environment variable: `Serilog__MinimumLevel__Override__Njord.Pipeline=Debug`. Log levels are read at startup.

## Startup validation

njord validates the configuration at startup and refuses to start if:

- No locations are defined
- A location has an empty name
- `Weather:ForecastDays` is outside 1 to 16
- The MQTT host is empty while MQTT is enabled
- PostgreSQL is selected without a connection string
- Projected API usage exceeds 80% of the monthly budget
