# Locations

Each location defines a geographic point for which njord fetches weather forecasts. At least one location is required.

## Options

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `Name` | string | yes | Human-readable name used in MQTT topics and HA device names |
| `Latitude` | double | yes | Decimal latitude (e.g. `51.85`) |
| `Longitude` | double | yes | Decimal longitude (e.g. `6.96`) |
| `Models` | string[] | no | Additional models for this location, merged with the global list |

## Examples

### Single location

```json
{
  "Njord": {
    "Locations": [
      {
        "Name": "Home",
        "Latitude": 51.85,
        "Longitude": 6.96
      }
    ],
    "Models": ["icon_eu", "ecmwf_ifs025"]
  }
}
```

### Multiple locations with per-location models

```json
{
  "Njord": {
    "Models": ["icon_global", "icon_eu", "ecmwf_ifs025", "gfs_seamless"],
    "Locations": [
      {
        "Name": "Berlin",
        "Latitude": 52.52,
        "Longitude": 13.41,
        "Models": ["icon_d2"]
      },
      {
        "Name": "Amsterdam",
        "Latitude": 52.37,
        "Longitude": 4.90,
        "Models": ["icon_d2", "knmi_harmonie_arome_netherlands"]
      }
    ]
  }
}
```

In this example, Berlin gets 5 models (the 4 global ones plus `icon_d2`) and Amsterdam gets 6 models (global plus `icon_d2` and `knmi_harmonie_arome_netherlands`). Per-location models are merged with the global list, deduplicated, and case-insensitive.

## Model coverage validation

At startup, njord checks whether each model covers the configured location by comparing against known geographic bounding boxes. If a model does not cover a location, njord logs a warning. The API will return HTTP 400 for models with no data at a location, so njord skips those gracefully.

::: tip
Use the [model catalog](/models) to find which models cover your region, or use the [Config Builder](/builder) to select locations and see compatible models.
:::

## Topic naming

The location name is slugified for MQTT topics: lowercased, non-alphanumeric characters replaced with underscores. For example, `"New York"` becomes `new_york` in topics like `njord/new_york/icon_eu/h3`.
