# Locations

Each location is a geographic point for which njord fetches Weather, Air Quality and Flood data. One persistent PollWorker runs per location. At least one location is required.

## Options

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `Name` | string | yes | Name used in MQTT topics, gRPC requests and HA device names |
| `Latitude` | double | yes | Decimal latitude (e.g. `47.05`) |
| `Longitude` | double | yes | Decimal longitude (e.g. `8.31`) |
| `Models` | string[] | no | Extra model ids for this location, merged with the global `Models` list |

## Examples

### Single location

```json
{
  "Njord": {
    "Locations": [
      { "Name": "Lucerne", "Latitude": 47.05, "Longitude": 8.31 }
    ],
    "Models": ["icon_eu"]
  }
}
```

### Multiple locations

```json
{
  "Njord": {
    "Models": ["icon_eu"],
    "Locations": [
      { "Name": "Lucerne", "Latitude": 47.05, "Longitude": 8.31, "Models": ["meteoswiss_icon_ch2"] },
      { "Name": "Berlin", "Latitude": 52.52, "Longitude": 13.41, "Models": ["icon_d2"] }
    ]
  }
}
```

Lucerne is validated against `icon_eu` and `meteoswiss_icon_ch2`, Berlin against `icon_eu` and `icon_d2`. Per-location models are merged with the global list, deduplicated and case-insensitive.

## Budget impact

Every location adds three API calls per poll cycle (Weather, Air Quality, Flood). Two locations at the default 60-minute interval project to 4,320 calls per month. See [Budget](./budget).

## Topic naming

The location name is slugified for MQTT topics: lowercased, non-alphanumeric characters replaced with underscores. `"New York"` becomes `new_york`, as in `njord/new_york/weather/state`.

::: tip
Use the [model catalog](/models) to find models for your region, or the [Config Builder](/builder).
:::
