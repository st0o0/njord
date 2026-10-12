# MQTT

MQTT settings control how njord connects to the broker and publishes discovery and state payloads.

## Configuration

```json
{
  "Njord": {
    "Mqtt": {
      "Host": "192.168.1.100",
      "Port": 1883,
      "Username": "mqtt-user",
      "Password": "mqtt-pass",
      "DiscoveryPrefix": "homeassistant",
      "BaseTopic": "njord"
    }
  }
}
```

## Options

| Option | Default | Required | Description |
|--------|---------|----------|-------------|
| `Host` | `""` | yes | Hostname or IP of the MQTT broker |
| `Port` | `1883` | no | MQTT broker port |
| `Username` | `null` | no | Broker username for authentication |
| `Password` | `null` | no | Broker password for authentication |
| `DiscoveryPrefix` | `"homeassistant"` | no | MQTT Discovery prefix used by Home Assistant |
| `BaseTopic` | `"njord"` | no | Root topic for all njord state messages |

## Authentication

::: warning
Avoid putting passwords directly in `appsettings.json`. Use an environment variable instead:

```bash
-e Njord__Mqtt__Password=your-secret-password
```
:::

## Discovery

njord publishes retained device configuration payloads to `{DiscoveryPrefix}/device/{deviceId}/config`. Home Assistant automatically creates devices and sensors from these payloads.

Discovery payloads are re-published on Home Assistant restart (detected via the `homeassistant/status` birth message).

## Availability

njord uses a Last Will and Testament (LWT) message on `{BaseTopic}/status`:
- `online`: published on connect
- `offline`: published by the broker if njord disconnects unexpectedly

Sensors also use `expire_after` set to 2x the poll interval. If no update arrives within that window, Home Assistant marks the sensor as unavailable.

## Topic reference

See the [MQTT reference](/mqtt-reference) for the complete topic scheme and payload format.
