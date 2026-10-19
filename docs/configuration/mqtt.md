# MQTT

MQTT is an optional output for consumers other than Home Assistant (Node-RED, Grafana, custom dashboards). It is disabled by default. njord publishes retained state only, no MQTT Discovery; Home Assistant uses the [custom integration](/home-assistant/) over gRPC.

## Configuration

```json
{
  "Njord": {
    "Mqtt": {
      "Enabled": true,
      "Host": "192.168.1.100",
      "Port": 1883,
      "Username": "mqtt-user",
      "Password": "mqtt-pass",
      "BaseTopic": "njord"
    }
  }
}
```

## Options

| Option | Default | Required | Description |
|--------|---------|----------|-------------|
| `Enabled` | `false` | no | Switches the MQTT publisher on |
| `Host` | `""` | when enabled | Hostname or IP of the broker |
| `Port` | `1883` | no | Broker port |
| `Username` | `null` | no | Broker username |
| `Password` | `null` | no | Broker password |
| `DiscoveryPrefix` | `"homeassistant"` | no | Prefix of the Home Assistant status topic njord subscribes to (`{DiscoveryPrefix}/status`); no discovery payloads are published |
| `BaseTopic` | `"njord"` | no | Root topic for all state messages |

## Topics

State is published per domain and parameter, and per alert type:

```
njord/lucerne/weather/state
njord/lucerne/weather/temperature
njord/lucerne/air-quality/pm25
njord/lucerne/flood/discharge
njord/lucerne/solar/direct_radiation
njord/lucerne/alerts/frost
```

The full scheme and payloads are in the [MQTT reference](/mqtt-reference).

## Authentication

::: warning
Do not put passwords in `appsettings.json`. Use an environment variable:

```bash
-e Njord__Mqtt__Password=your-secret-password
```
:::

## Availability

njord uses a Last Will and Testament on `{BaseTopic}/status`:

- `online`: published after every connect
- `offline`: published by the broker if njord disconnects unexpectedly
