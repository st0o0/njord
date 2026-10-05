# Home Assistant Integration

The njord custom integration connects Home Assistant to the njord weather service via gRPC. Data flows in real time through gRPC streams, so entities update as soon as njord processes a new poll cycle. There is no polling delay on the HA side.

The integration source code lives in the **[ha-njord](https://github.com/st0o0/ha-njord)** repository.

## Installation

### HACS (recommended)

1. Open HACS, then go to three dots > **Custom repositories**
2. Add `https://github.com/st0o0/ha-njord` as **Integration**
3. Search for **njord Weather** and install
4. Restart Home Assistant

### Manual

Copy the `custom_components/njord` directory from [ha-njord](https://github.com/st0o0/ha-njord) into your Home Assistant `config/custom_components/` folder and restart.

## Setup

1. Go to **Settings > Devices & Services > Add Integration**
2. Search for **njord Weather**
3. Enter the host and gRPC port of your njord instance (default port: 8081)
4. The integration auto-discovers all configured locations and models

## Device structure

The integration creates two types of devices:

**Location device** (one per configured location, e.g. "njord Home"):
- Weather entities for each model and the consensus
- Enrichment sensors (alerts, indices, trends, derived, history)
- Weather alert event entity
- Inversion binary sensor

**Server device** (one per njord instance, e.g. "njord Server"):
- Trigger Poll button
- Stream connectivity sensors (forecast, enrichment, config)
- API usage sensors (monthly, daily)
- Version and uptime sensors
- Per-target poll state sensors

Find all njord devices under **Settings > Devices & Services > njord Weather**.

## Enabling disabled entities

Many enrichment sensors are disabled by default to keep the entity list manageable. To enable them:

1. Go to **Settings > Devices & Services > njord Weather**
2. Click the location device
3. Find the entity you want, click it
4. Toggle **Enabled** on

Alternatively, use **Settings > Entities**, filter by "njord", and bulk-enable the sensors you need.

## Recorder exclude

The integration can create many entities per location (40+ with all enrichments enabled). To prevent your Home Assistant database from growing rapidly, add a recorder exclude:

```yaml
# configuration.yaml
recorder:
  exclude:
    entity_globs:
      - sensor.njord_*
      - binary_sensor.njord_*
```

::: tip
This excludes njord sensors from the long-term history database while keeping them fully functional for automations, dashboards, and current state display. If you want history for specific sensors, use an `include` override for those.
:::

## Entity availability

Entities become unavailable when:

- The gRPC connection to njord is lost (the stream connectivity binary sensors turn off)
- njord has no data for a location or model
- An enrichment feature is disabled in the njord configuration

The three stream connectivity sensors (`binary_sensor.njord_*_stream`) show the real-time connection state. If all three are off, the integration cannot reach the njord server.
