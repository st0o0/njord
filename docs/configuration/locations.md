# Locations

Each location is a geographic point for which njord fetches data for every enabled domain (Forecast, Ensemble, Air Quality, Flood, Marine, Seasonal, Satellite). One persistent PollWorker runs per location. At least one location is required.

## Options

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `Name` | string | yes | Name used in gRPC requests and HA device names |
| `Latitude` | double | yes | Decimal latitude (e.g. `47.05`) |
| `Longitude` | double | yes | Decimal longitude (e.g. `8.31`) |

## Examples

### Single location

```json
{
  "Njord": {
    "Locations": [
      { "Name": "Lucerne", "Latitude": 47.05, "Longitude": 8.31 }
    ]
  }
}
```

### Multiple locations

```json
{
  "Njord": {
    "Locations": [
      { "Name": "Lucerne", "Latitude": 47.05, "Longitude": 8.31 },
      { "Name": "Berlin", "Latitude": 52.52, "Longitude": 13.41 }
    ]
  }
}
```

## Budget impact

The calls a location adds per poll cycle depend on the enabled features: the polled domains are the union of the domains they read, and the Forecast and Ensemble calls scale with the number of variables, forecast days and models. Every location multiplies that cost. See [Budget](./budget) for the formula and the Config Builder for an estimate. See [Budget](./budget).


::: tip
The [Config Builder](/builder) generates a complete configuration.
:::
