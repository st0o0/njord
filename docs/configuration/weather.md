# Weather

Options for the Weather domain under `Njord:Weather`. The model is fixed: njord uses the Open-Meteo Ensemble Mean, and the variable list is built into the client. Air Quality, Flood and Marine use fixed variable sets too.

```json
{
  "Njord": {
    "Weather": {
      "ForecastDays": 4
    }
  }
}
```

As environment variables: `Njord__Weather__IncludeSolar=true`, `Njord__Weather__ForecastDays=4`.

| Option | Default | Description |
|--------|---------|-------------|
| `ForecastDays` | `4` | Days of forecast to request (1 to 16) |

## API call weight

The weather request asks only for the hourly variables the enabled enrichments need (the union of their requirements). Open-Meteo counts a request as `ceil(variables / 10)` calls, doubled for every 14 forecast days started:

```
weather weight = ceil(variables / 10) x ceil(ForecastDays / 14)
```

The weight is charged to the Weather domain only; every other domain costs one call per location and cycle. See the [budget](./budget) projection.
