# Configuration

njord is configured via `appsettings.json` or environment variables. All settings live under the `Njord` section.

## Structure overview

```json
{
  "Njord": {
    "PollInterval": "01:00:00",
    "ForecastDays": 4,
    "Horizons": [3, 6, 12, 24, 48, 72],
    "Locations": [ ... ],
    "Models": [ ... ],
    "Parameters": { ... },
    "Mqtt": { ... },
    "Enrichment": { ... },
    "Persistence": { ... },
    "BudgetOverride": null,
    "DiscoveryInterval": "00:20:00"
  }
}
```

| Setting | Default | Description |
|---------|---------|-------------|
| `PollInterval` | `01:00:00` (60 min) | How often njord polls Open-Meteo for new forecasts |
| `ForecastDays` | `4` | Number of days to request from the API (1 to 16) |
| `DiscoveryInterval` | `00:20:00` (20 min) | Poll interval for a model whose update cycle is not learned yet (discovery phase); once the cycle is known, polls follow it |
| `PersistencePath` | `data/njord.db` | Path to the SQLite database file |

## Environment variable overrides

Every setting can be overridden via environment variables using double-underscore notation:

```
Njord__PollInterval=00:30:00
Njord__Mqtt__Host=192.168.1.100
Njord__Locations__0__Name=Home
Njord__Locations__0__Latitude=47.05
Njord__Models__0=icon_eu
Njord__Models__1=ecmwf_ifs025
Njord__Enrichment__Consensus__Method=TrimmedMean
```

Array items use zero-based indices (`__0__`, `__1__`, etc.).

::: tip
Use environment variables for secrets like `Njord__Mqtt__Password` instead of putting them in the config file.
:::

## Runtime changes (override file)

Settings changed at runtime (for example from the Home Assistant integration through the gRPC AdminService) are stored in `data/appsettings.Override.json`. njord creates the file on the first change, writes it atomically, and loads it last, so its values win over `appsettings.json` and environment variables. Mount `data/` as a volume to keep the changes across container restarts.

If the file cannot be read or parsed (truncated, empty, not a JSON object):

- njord still starts, using `appsettings*.json` and environment variables, and logs an error that names the file and the problem. The file is left as it is, so you can repair it in place; a repaired file is picked up automatically.
- If the file becomes unreadable while njord runs, the last good values stay in effect and an error is logged.
- The next configuration change moves the broken file aside to `appsettings.Override.json.corrupt-<UTC timestamp>` and writes a new file with the last good values plus the change. njord never deletes the broken file; remove the `.corrupt-*` copy once you no longer need it.

Comments and trailing commas are accepted when you edit the file by hand, but njord does not keep comments when it rewrites the file. A file that is valid JSON but contains invalid values still fails [startup validation](#startup-validation).

## Configuration sections

- [Locations](./locations): where to fetch forecasts for
- [Models](./models): which weather models to poll
- [Horizons](./horizons): forecast time offsets
- [Parameters](./parameters): which weather variables to request
- [Enrichment](./enrichment): consensus, alerts, derived metrics, and more
- [MQTT](./mqtt): broker connection and topic settings
- [Persistence](./persistence): SQLite or PostgreSQL storage
- [Budget](./budget): Open-Meteo rate limits and usage projection

## Logging

Log levels live in the `Serilog` section, outside `Njord`. Every log line shows its category, the full type name of the class that wrote it: njord's actors and services log as their class (`[Njord.Pipeline.PollWorker]`), Akka.NET's internals as `Akka.*` types. The shipped `"Akka": "Warning"` override therefore only quiets Akka itself. An override key matches its category and every category below it:

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

`Njord.Pipeline` shows the Debug logs of the poll manager, the poll workers and the analysis pipeline; `Njord.Mqtt.MqttStatePublisher` adds element-level tracing of the MQTT state stream (its `mqtt-state-in` and `mqtt-state-out` stages). Environment variables work as well: `Serilog__MinimumLevel__Override__Njord.Pipeline=Debug`.

The actor system creates log events only down to the lowest level configured in `MinimumLevel` (`Default` and every override), read once at startup; `Information` is the floor when nothing lower is set. Serilog level switches (`LevelSwitches`, `ControlledBy`) are not taken into account for that floor. Changing log levels needs a restart.

## Startup validation

njord validates the entire configuration at startup and refuses to start if:

- No locations or models are defined
- The MQTT host is empty
- Horizons are outside the valid range (1 to 96)
- PostgreSQL is selected without a connection string
- Projected API usage exceeds 80% of the monthly budget
- Parameter groups, extras, or excludes reference unknown parameters
