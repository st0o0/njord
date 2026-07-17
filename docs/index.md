---
layout: home

hero:
  name: njord
  text: Multi-model weather intelligence for Home Assistant
  tagline: Polls 50+ weather models, enriches forecasts with consensus, alerts, and trends, and publishes everything to Home Assistant via MQTT Discovery.
  image:
    src: /logo.svg
    alt: njord
  actions:
    - theme: brand
      text: Getting Started
      link: /getting-started
    - theme: alt
      text: Config Builder
      link: /builder

features:
  - title: Multi-model forecasts
    details: Poll multiple weather models per location — ICON, ECMWF, GFS, UKMO, MeteoSwiss, and regional models — each published as a dedicated Home Assistant device with hourly and daily sensors.
  - title: Enrichment pipeline
    details: Consensus forecasts, weather alerts, derived values (Beaufort, wind chill, comfort), trend analysis, activity indices, energy optimization, and forecast accuracy tracking — all configurable.
  - title: Weather alerts
    details: Frost, heat, storm, heavy rain, UV, fog, snow, pressure drop, and thunderstorm alerts derived directly from model data — no third-party alert service required.
  - title: Low resource usage
    details: Runs as a single .NET container on any Docker host. SQLite persistence by default, no external database needed. Respects Open-Meteo free-tier rate limits out of the box.
---
