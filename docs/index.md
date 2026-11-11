---
layout: home

hero:
  name: njord
  text: Multi-domain weather intelligence for Home Assistant
  tagline: Polls six Open-Meteo APIs per location, enriches the data with 89 features (alerts, scores, conditions, trends and forecasts) and streams everything to Home Assistant over gRPC.
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
  - title: Six domains per location
    details: "Forecast (high-resolution deterministic), Ensemble (confidence and model comparison), Air Quality (PM2.5, ozone, pollen), Flood (river discharge), Marine (waves, currents), and Seasonal (monthly and weekly outlook)."
  - title: 89 features across all domains
    details: "30 alerts (weather, air quality, flood, marine), 23 observations, 23 conditions, 7 scores (BBQ, laundry, outdoor, sleep quality, allergy, asthma, spray window), 4 forecasts (model comparison, seasonal outlook, growing degrees), 1 trend, and 1 weather entity."
  - title: Feature-driven configuration
    details: You switch features on, njord derives which API domains to poll and which variables to request. The Config Builder shows the cost per feature. A default setup with one location uses less than 1% of the free-tier budget.
  - title: Static configuration, low footprint
    details: One .NET container, SQLite by default, configured through environment variables or appsettings.json. No external database, no API key, no account needed.
---
