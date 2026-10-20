---
layout: home

hero:
  name: njord
  text: Multi-domain weather intelligence for Home Assistant
  tagline: A .NET proxy for Open-Meteo. It polls the Ensemble Mean, Air Quality and Flood APIs per location, enriches the data with alerts (including cross-domain ones) and serves the results to Home Assistant over gRPC. MQTT is an optional alternative for other consumers.
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
    details: The ha-njord custom integration streams domain data and alerts over gRPC, so entities update as soon as a poll cycle is processed.
  - title: Three domains per location
    details: Weather (Ensemble Mean, with solar radiation when Weather:IncludeSolar is enabled), Air Quality (PM2.5, PM10, ozone, pollen) and Flood (river discharge). One PollWorker per location fetches all three each cycle.
  - title: Alerts across domains
    details: 14 weather alerts with confidence from ensemble spread, pollen and PM2.5 alerts for air quality, and flood risk alerts. FloodRain combines heavy forecast rain with rising river discharge.
  - title: Domain handler pipeline
    details: A ProjectionEngine keeps one LocationForecast per location. Each domain handler declares the domains that trigger it and re-runs only when one of them changes.
  - title: Static configuration, low footprint
    details: One .NET container, SQLite by default, configured through appsettings.json or environment variables. A default setup with one location stays far below the free-tier limits.
  - title: MQTT alternative
    details: Optional retained state topics per domain (njord/{location}/{domain}/{parameter}) and alert topics for Node-RED or custom dashboards. Disabled by default.
---
