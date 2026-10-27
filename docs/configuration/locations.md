# Locations

Each location is a geographic point for which njord fetches Forecast, Air Quality and Flood data. One persistent PollWorker runs per location. At least one location is required.

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

Every location adds three API calls per poll cycle (Forecast, Air Quality, Flood). Two locations at the default 60-minute interval project to 4,320 calls per month. See [Budget](./budget).


::: tip
The [Config Builder](/builder) generates a complete configuration.
:::
