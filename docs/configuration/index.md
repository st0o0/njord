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
| `ForecastDays` | `4` | Number of days to request from the API (1--16) |
| `DiscoveryInterval` | `00:20:00` (20 min) | How often HA MQTT discovery payloads are re-published |
| `PersistencePath` | `data/njord-journal.db` | Path to the SQLite journal file |

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

## Configuration sections

- [Locations](./locations) — where to fetch forecasts for
- [Models](./models) — which weather models to poll
- [Horizons](./horizons) — forecast time offsets
- [Parameters](./parameters) — which weather variables to request
- [Enrichment](./enrichment) — consensus, alerts, derived metrics, and more
- [MQTT](./mqtt) — broker connection and topic settings
- [Persistence](./persistence) — SQLite or PostgreSQL storage
- [Budget](./budget) — Open-Meteo rate limits and usage projection

## Startup validation

njord validates the entire configuration at startup and refuses to start if:

- No locations or models are defined
- The MQTT host is empty
- Horizons are outside the valid range (1--96)
- PostgreSQL is selected without a connection string
- Projected API usage exceeds 80% of the monthly budget
- Parameter groups, extras, or excludes reference unknown parameters
