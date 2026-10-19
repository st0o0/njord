# Diagnostics

The integration supports Home Assistant's built-in diagnostics download for troubleshooting.

## Downloading diagnostics

1. Go to **Settings > Devices & Services > njord Weather**
2. Click the three-dot menu on the integration card
3. Select **Download diagnostics**

## What's included

| Section | Content |
|---------|---------|
| `locations` | Configured location names |
| `forecasts` | Current domain data per location (Weather, Air Quality, Flood, Solar): timestamps and update status |
| `enrichments` | Current alerts per location |
| `active_enrichments` | Which enrichment features are enabled on the server |
| `server` | Server status (version, uptime, budget usage, per-location poll state), only if the status coordinator is active |

**Sensitive data:** the njord server host address is redacted automatically. No API keys or credentials are included (njord uses none).

## Server-side checks

If entities are unavailable, check the server too:

- `/healthz` on port 8080 reports `pipeline`, `budget`, `journal` and `data-directory` (plus `mqtt-connection` when MQTT is enabled)
- The `binary_sensor.njord_*_stream` entities show the gRPC stream state
- `sensor.njord_{location}_target` shows each location's next poll time and phase
