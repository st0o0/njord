# Configuration

njord is configured through `appsettings.json` and environment variables. All settings live under the `Njord` section.

Configuration is **static**: it is read once at startup. There is no runtime configuration API and no override file. To change a setting, edit your configuration or environment and restart the container; a restart takes seconds.

## Structure overview

```json
{
  "Njord": {
    "ForecastDays": 4,
    "Horizons": [3, 6, 12, 24, 48, 72],
    "Locations": [ ... ],
    "Models": [ ... ],
    "Parameters": { ... },
    "Polling": {
      "PollInterval": "01:00:00",
      "DiscoveryInterval": "00:20:00",
      "BudgetOverride": null
    },
    "OpenMeteo": { "BaseUrl": "https://api.open-meteo.com/" },
    "Storage": { "PersistencePath": "data/njord.db" },
    "Persistence": { ... },
    "Mqtt": { ... },
    "Enrichment": { "Alerts": { ... } }
  }
}
```

A minimal setup for Lucerne:

```json
{
  "Njord": {
    "Locations": [{ "Name": "Lucerne", "Latitude": 47.05, "Longitude": 8.31 }],
    "Models": ["icon_eu"]
  }
}
```

| Setting | Default | Description |
|---------|---------|-------------|
| `Polling:PollInterval` | `01:00:00` | How often each PollWorker fetches all domains of its location |
| `Polling:DiscoveryInterval` | `00:20:00` | Poll interval until a worker has learned the update cycle of the data |
| `ForecastDays` | `4` | Days of weather forecast to request (1 to 16) |
| `OpenMeteo:BaseUrl` | `https://api.open-meteo.com/` | Open-Meteo endpoint for the weather domain (self-hosted instance or test double) |
| `Storage:PersistencePath` | `data/njord.db` | SQLite database file |

## Defaults

All defaults are property initializers on the options classes. `appsettings.json` in the image only holds logging and Kestrel settings. [`appsettings.Example.json`](https://github.com/st0o0/njord/blob/main/src/Njord/appsettings.Example.json) is a template with deployment-specific keys only.

List-valued defaults (`Horizons`, `Parameters.Groups`, the alert threshold arrays) are built in: a list you configure replaces the built-in list completely, it is never merged by position. To go back to a default, remove your key.

## Environment variable overrides

Use double-underscore notation; array items use zero-based indices:

```
Njord__Polling__PollInterval=00:30:00
Njord__Locations__0__Name=Lucerne
Njord__Locations__0__Latitude=47.05
Njord__Locations__0__Longitude=8.31
Njord__Models__0=icon_eu
Njord__Parameters__Groups__0=Weather
Njord__Parameters__Groups__1=Solar
Njord__Enrichment__Alerts__Enabled=true
Njord__Mqtt__Host=192.168.1.100
```

::: tip
Use environment variables for secrets like `Njord__Mqtt__Password`.
:::

## Configuration sections

- [Locations](./locations): where to fetch forecasts for
- [Models](./models): the model list and the Ensemble default
- [Horizons](./horizons): forecast time offsets
- [Parameters](./parameters): parameter groups (Weather, Solar, Soil)
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

- No locations or no model ids are defined
- A location has an empty name
- Horizons are outside 1 to 96, or `ForecastDays` outside 1 to 16
- Parameter groups, extras or excludes reference unknown parameters
- The MQTT host is empty while MQTT is enabled
- PostgreSQL is selected without a connection string
- Projected API usage exceeds 80% of the monthly budget
