# What is njord?

njord is a weather intelligence service for [Home Assistant](https://www.home-assistant.io/). It runs as a single Docker container, polls the free [Open-Meteo](https://open-meteo.com/) APIs at regular intervals, and streams enriched weather data to Home Assistant over gRPC.

<likec4-view view-id="index"></likec4-view>

## What you get

Out of the box, njord gives you **21 features** per location:

- **15 weather alerts** (frost, heat, storm, heavy rain, UV, fog, snow, pressure drop, thunderstorm, ice, wind chill, visibility, tropical night, humidity, rain probability)
- **Commute safety** condition
- **Pollen** and **PM2.5** air quality alerts
- **Forecast** and **Ensemble model comparison**
- **Weather entity** with hourly forecast

That is about **45 Home Assistant entities** per location with defaults. Enable more features to add lifestyle scores, energy monitoring, flood risk, marine conditions, seasonal outlook and more (89 features total, up to ~70 entities per location).

## How it works

1. njord polls Open-Meteo for the domains your enabled features need (Forecast, Ensemble, Air Quality, Flood, Marine, Seasonal)
2. Feature rules evaluate the data and produce alerts, scores, conditions, trends and forecasts
3. Results stream to Home Assistant over gRPC, where the [ha-njord](https://github.com/st0o0/ha-njord) integration turns them into native entities

No API key needed. Open-Meteo's free tier (300,000 requests/month) is more than enough for typical setups.

## Prerequisites

- **Docker** (or Podman) on any Linux, macOS or Windows host
- **Home Assistant** with [HACS](https://hacs.xyz/) installed

## Next

- [Installation](/getting-started/installation) -- get njord running in Docker
- [Connect Home Assistant](/getting-started/home-assistant) -- install ha-njord and add the integration
- [Features & Budget](/getting-started/features) -- understand what features are and how the budget works
