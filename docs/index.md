---
layout: home

hero:
  name: njord
  text: Weather forecasts in Home Assistant
  tagline: Open-Meteo weather API → MQTT bridge for Home Assistant
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
    details: Poll multiple weather models per location — ICON, ECMWF, GFS, UKMO, and regional models — and publish each as a dedicated Home Assistant device.
  - title: Consensus computation
    details: Combine forecasts across models using median or trimmed-mean aggregation, with spread and agreement metadata so you know how confident the forecast is.
  - title: Weather alerts
    details: Frost, heat, storm, heavy rain, UV, fog, snow, pressure drop, and thunderstorm alerts derived directly from model data — no third-party alert service required.
  - title: Low resource usage
    details: Runs as a single .NET container on any Docker host. SQLite persistence by default, no external database needed. Respects Open-Meteo free-tier rate limits out of the box.
---
