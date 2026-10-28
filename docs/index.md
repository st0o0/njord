---
layout: home

hero:
  name: njord
  text: Multi-domain weather intelligence for Home Assistant
  tagline: Polls seven Open-Meteo APIs per location, enriches the data with 66 features (alerts, scores, conditions, trends and forecasts) and streams everything to Home Assistant over gRPC.
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
    details: The ha-njord custom integration streams domain data and enrichments over gRPC, so entities update as soon as a poll cycle is processed.
  - title: Seven domains per location
    details: "Forecast (high-resolution deterministic), Ensemble (confidence and model comparison), Air Quality (PM2.5, ozone, pollen), Flood (river discharge), Marine (waves, currents), Seasonal (monthly outlook) and Satellite (observed solar irradiance)."
  - title: 66 features across all domains
    details: "22 weather alerts with ensemble confidence, air quality and pollen alerts, flood and coastal flood warnings, fire risk, smog detection, lifestyle scores (BBQ, laundry, exercise, commute), gardening tools (soil moisture, evapotranspiration, growing degrees), energy monitoring (solar yield, hub-height wind) and more."
  - title: Feature-driven configuration
    details: You switch features on, njord derives which API domains to poll and which variables to request. The Config Builder shows the cost per feature. A default setup with one location uses less than 1% of the free-tier budget.
  - title: Static configuration, low footprint
    details: One .NET container, SQLite by default, configured through environment variables or appsettings.json. No external database, no API key, no account needed.
---
