---
layout: home

hero:
  name: njord
  text: Multi-model weather intelligence for Home Assistant
  tagline: Poll 50+ weather models per location, enrich forecasts with consensus, alerts, trends, and activity indices, and stream everything to the ha-njord custom integration via gRPC. MQTT is available as an alternative for non-HA consumers.
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
  - title: Native Home Assistant integration
    details: The ha-njord custom integration connects via gRPC streaming for real-time updates. Creates native weather, sensor, binary sensor, event, and button entities with no polling delay.
  - title: Multi-model forecasts
    details: Poll multiple weather models per location (ICON, ECMWF, GFS, UKMO, MeteoSwiss, and 40+ regional models). Each model becomes a dedicated weather entity with hourly and daily forecasts.
  - title: 14 weather alerts
    details: Frost, heat, storm, heavy rain, UV, fog, snow, pressure drop, thunderstorm, ice, wind chill, visibility, tropical night, and humidity alerts with severity levels, confidence scores, and event notifications.
  - title: Enrichment pipeline
    details: Consensus forecasts, derived values (Beaufort, wind chill, dewpoint comfort), trend analysis, 8 activity indices (outdoor, running, cycling, BBQ, and more), VPD, and forecast accuracy tracking.
  - title: Low resource usage
    details: Runs as a single .NET container on any Docker host. SQLite persistence by default, no external database needed. Respects Open-Meteo free-tier rate limits out of the box.
  - title: MQTT alternative
    details: Optional MQTT egress publishes forecasts and enrichments via MQTT Discovery for Node-RED, custom dashboards, or other consumers. Disabled by default.
---
