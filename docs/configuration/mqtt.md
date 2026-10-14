# MQTT

MQTT settings control how njord connects to the broker and publishes state payloads. njord does not publish MQTT Discovery payloads; Home Assistant connects through the [custom integration](/home-assistant/) over gRPC.

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
| `DiscoveryPrefix` | `"homeassistant"` | no | Prefix of the Home Assistant status topic njord subscribes to (`{DiscoveryPrefix}/status`); no discovery payloads are published |
| `BaseTopic` | `"njord"` | no | Root topic for all njord state messages |

## Authentication

::: warning
Avoid putting passwords directly in `appsettings.json`. Use an environment variable instead:

```bash
-e Njord__Mqtt__Password=your-secret-password
```
:::

## Availability

njord uses a Last Will and Testament (LWT) message on `{BaseTopic}/status`:
- `online`: published on connect
- `offline`: published by the broker if njord disconnects unexpectedly

## Topic reference

See the [MQTT reference](/mqtt-reference) for the complete topic scheme and payload format.
