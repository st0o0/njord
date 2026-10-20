# Options

The integration options flow lets you configure the status polling interval without removing and re-adding the integration.

Go to **Settings > Devices & Services > njord Weather > Configure**.

## Status poll interval

Controls how often the integration polls the njord status endpoint (version, uptime, API usage, per-location poll state). Default: 30 seconds. Range: 10 to 300 seconds.

Changing only the poll interval does not reload the integration.

## Server-side settings

What njord fetches and computes is configured on the server, not in Home Assistant. The server configuration is static (`appsettings.json` and environment variables) and applied at startup; restart the njord container after a change.

| Setting | Effect in Home Assistant |
|---------|--------------------------|
| `Njord:Locations` | One set of devices and entities per location |
| `Njord:Features:<Category>:<Name>:Enabled` | One feature (alert, score, condition, ...) and its entities |
| `Njord:Polling:PollInterval` | How often new data arrives |

See [Configuration](/configuration/).

::: warning ha-njord update pending
The options flow is being aligned with the multi-domain server. The status poll interval is the only option that applies today.
:::
