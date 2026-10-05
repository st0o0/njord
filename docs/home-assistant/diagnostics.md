# Diagnostics

The integration supports Home Assistant's built-in diagnostics download for troubleshooting.

## Downloading diagnostics

1. Go to **Settings > Devices & Services > njord Weather**
2. Click the three-dot menu on the integration card
3. Select **Download diagnostics**

## What's included

The diagnostics file contains:

| Section | Content |
|---------|---------|
| `locations` | Configured location names |
| `models` | Model list per location |
| `forecasts` | Current forecast data per location/model (timestamps, update status) |
| `enrichments` | Current enrichment data per location (alerts, indices, trends, derived, consensus, history) |
| `active_enrichments` | Which enrichment features are currently enabled on the server |
| `server` | Server status (version, uptime, phase, budget usage) — only if the status coordinator is active |

**Sensitive data**: The njord server host address is redacted automatically. No API keys or credentials are included (njord doesn't use any).
