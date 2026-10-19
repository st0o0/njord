---
layout: home

hero:
  name: njord
  text: Multi-domain weather intelligence for Home Assistant
  tagline: A .NET weather proxy that polls Open-Meteo's Ensemble and domain-specific APIs (weather, air quality, flood, solar) per location, enriches them with alerts, and serves the results to Home Assistant via gRPC. MQTT is available as an alternative for non-HA consumers.
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
  - title: Multi-domain data
    details: Polls Open-Meteo's Ensemble Mean API for weather, plus Air Quality, Flood, and Solar endpoints per location. One API call replaces polling 5–8 individual models. More data, fewer requests.
  - title: 14 weather alerts
    details: Frost, heat, storm, heavy rain, UV, fog, snow, pressure drop, thunderstorm, ice, wind chill, visibility, tropical night, and humidity alerts with severity levels and confidence from ensemble spread.
  - title: Projection group pipeline
    details: Domain-specific enrichments via typed projection groups. Each group declares its required domains and runs reactively only when those domains are updated. Extensible for cross-domain enrichments.
  - title: Low resource usage
    details: Runs as a single .NET container on any Docker host. SQLite persistence by default, no external database needed. Respects Open-Meteo free-tier rate limits out of the box.
  - title: MQTT alternative
    details: Optional MQTT egress publishes retained forecast and enrichment state for Node-RED, custom dashboards, or other consumers. Disabled by default.
---
