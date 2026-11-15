# Connect Home Assistant

The [ha-njord](https://github.com/st0o0/ha-njord) custom integration connects Home Assistant to your njord instance over gRPC.

## Install the integration

### HACS (recommended)

[![Open your Home Assistant instance and open a repository inside the Home Assistant Community Store.](https://my.home-assistant.io/badges/hacs_repository.svg)](https://my.home-assistant.io/redirect/hacs_repository/?owner=st0o0&repository=ha-njord&category=integration)

1. Click the button above, or open HACS > three dots > **Custom repositories** and add `https://github.com/st0o0/ha-njord` as **Integration**
2. Search for "njord Weather" and install
3. Restart Home Assistant

### Manual

Copy `custom_components/njord` from the [ha-njord repository](https://github.com/st0o0/ha-njord) to your Home Assistant `config/custom_components/` directory and restart.

## Add the integration

1. Go to **Settings > Devices & Services > Add Integration**
2. Search for **njord Weather**
3. Enter the host and gRPC port (default `8081`) of your njord instance

The integration connects immediately and creates entities for all enabled features.

## What you will see

Under **Settings > Devices & Services > njord Weather**:

**Per location:**
- Weather entity with hourly forecast
- Alert binary sensors (frost, heat, storm, etc.)
- Air quality and flood sensors
- Lifestyle conditions and scores
- Event entity for alert state changes

**Server device:**
- "Trigger Poll" button
- API usage, version, uptime sensors
- Stream connectivity sensor

With all defaults you get about **45 entities per location**. Enable more features to add lifestyle scores, energy monitoring, flood risk and more (about 70 entities with everything on).

## Next

- [Features & Budget](/getting-started/features) -- understand features and API usage
- [Entities reference](/home-assistant/entities) -- detailed list of all entity types
- [Options](/home-assistant/options) -- integration options in Home Assistant
