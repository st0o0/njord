# Options

The integration options flow lets you configure enrichment features, sensor push, and the status polling interval without removing and re-adding the integration.

Go to **Settings > Devices & Services > njord Weather > Configure**.

## Enrichment toggles

The first step lets you enable or disable enrichment groups. Changes take effect after reload.

| Group | What it controls |
|-------|-----------------|
| Consensus | Consensus weather entity per location |
| Alerts | Alert sensor entities and the alert event entity |
| Indices | Activity index sensors, VPD, and frost sensors |
| Trends | Weather trend sensor |
| Derived | Sunshine, diurnal amplitude, Beaufort, wind chill, dewpoint comfort |
| History | Model performance sensor |

Disabling a group removes the corresponding entities from Home Assistant. Enabling it again recreates them.

## Status poll interval

Controls how often the integration polls the njord server status endpoint (version, uptime, API usage, targets). Default: 30 seconds. Range: 10 to 300 seconds.

Changing only the poll interval does not trigger a reload of the integration.

## Sensor push

The second options step (Sensors) lets you configure sensor push — feeding indoor sensor readings from Home Assistant entities back to njord via gRPC.

| Field | Description |
|-------|-------------|
| Indoor temperature entity | HA entity to read indoor temperature from |
| Indoor humidity entity | HA entity to read indoor humidity from |

When configured, the integration subscribes to state changes on the selected entities and pushes readings to njord's SensorHub. njord uses these values to improve enrichment calculations (e.g., VPD, night ventilation index).

Leave the entity selectors empty to disable sensor push.

### push_sensor service

The integration also registers a `njord.push_sensor` service for programmatic use:

```yaml
action: njord.push_sensor
data:
  kind: indoor_temperature
  entity_id: sensor.living_room_temperature
  location: home           # optional if only one location is configured
  source: my_custom_source  # optional, defaults to entity_id
```

| Parameter | Required | Description |
|-----------|----------|-------------|
| `kind` | yes | `indoor_temperature` or `indoor_humidity` |
| `entity_id` | yes | HA entity to read the current value from |
| `location` | no | Target location (auto-resolved if only one exists) |
| `source` | no | Source identifier sent to njord (defaults to entity_id) |
