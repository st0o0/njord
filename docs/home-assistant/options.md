# Options

The integration options flow lets you configure the status polling interval without removing and re-adding the integration.

Go to **Settings > Devices & Services > njord Weather > Configure**.

::: warning ha-njord redesign pending
The options UI must be updated for njord's new architecture. Runtime configuration (enrichment toggles, sensor push) has been removed from njord — configuration is now static via environment variables or appsettings.json.
:::

## Enrichment toggles

| Group | What it controls |
|-------|-----------------|
| Alerts | Alert sensor entities and the alert event entity |

Other enrichment features (consensus, indices, trends, derived) were removed in the v2 redesign. Alerts are the only enrichment group currently available.

## Status poll interval

Controls how often the integration polls the njord server status endpoint (version, uptime, API usage, targets). Default: 30 seconds. Range: 10 to 300 seconds.

Changing only the poll interval does not trigger a reload of the integration.
