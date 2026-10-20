## Why

Njord answers "what is the weather over the next days". Seasonal outlook adds the long-range piece: "next month is expected 2°C warmer and 15% drier than normal." Open-Meteo provides a free Seasonal Forecast API (ECMWF SEAS5, 51 members, monthly values up to six months ahead) that no other HA integration exposes. The anomalies are delivered directly, so no baseline mechanism is needed.

## What Changes

- Seasonal becomes a proper domain: `DomainKind.Seasonal`, `TimeResolution.Monthly`, `SeasonalParams` and `SeasonalPoint` in `Njord.Forecast/Seasonal`; `DomainRequirements.Seasonal`, `LocationForecast.Seasonal`.
- New `ISeasonalClient`/`SeasonalClient` (`seasonal-api.open-meteo.com/v1/seasonal`, monthly variables supplied by the gateway), dispatched by `OpenMeteoGateway` like Weather/AirQuality/Flood/Marine; `SeasonalResponse` maps to `DomainForecast<SeasonalPoint>`.
- `PipelineEvent.SeasonalDataReceived` carries the data through the pipeline; the PollWorker learns the roughly daily update cycle through Discovery; budget and health work unchanged.
- `SeasonalOutlookHandler` (`IDomainHandler`, `FeatureKind.SeasonalOutlook`, Insight, off by default) turns the stored months into `PipelineEvent.SeasonalOutlookComputed` with `MonthlyOutlook` entries (month "2026-11", TempAnomaly in °C, PrecipAnomaly in %, Confidence High/Medium/Low from the lead time).
- `SeasonalOptions` (`Njord:Seasonal:ForecastMonths`, default 3, 1..6).
- `BudgetCalculator` counts Seasonal as 30 calls per month and location instead of once per poll cycle; the Config Builder mirrors it.
- gRPC + MQTT wire support via `LifestyleMappings.ToLifestyleUpdate()` (`LifestyleKind.SeasonalOutlook`); proto `Domain` enum gains `DOMAIN_SEASONAL`.
- Config Builder (`docs/data/features.json`, `budget.ts`), `docker-compose.example.yml`, docs, `AGENTS.md`.
- ha-njord (separate repository): seasonal outlook sensor and toggle, follow-up.

## Baseline

The API provides `temperature_2m_anomaly` (K) and `precipitation_anomaly` (mm) against the SEAS5 model climatology; `precipitation_mean` (mm) gives the normal month for the percent conversion (`mean - anomaly`). Verified against the live API.

## Non-goals

- Sub-monthly resolution (weekly outlook) and Extreme Forecast Index interpretation.
- Raw seasonal data on the gRPC/MQTT wire (only the computed outlook is delivered).
- Resurrecting any Climate Anomaly component.

## Capabilities

### New Capabilities

- `seasonal-outlook`: Seasonal domain, ISeasonalClient, SeasonalOutlookHandler, monthly anomaly outlook.

### Modified Capabilities

- `feature-catalog`: `SeasonalOutlook` feature added.

## Impact

- **Njord.Forecast**: `DomainKind.Seasonal`, `TimeResolution.Monthly`, `Seasonal/`.
- **Njord.Core**: `DomainRequirements`, `LocationForecast`, `SeasonalOptions`, `BudgetCalculator`, `FeatureKind.SeasonalOutlook`.
- **Njord.OpenMeteo**: client, response, mappings, gateway dispatch, registration.
- **Njord.Pipeline / Njord.Polling / Njord.Grpc**: event mapping, telemetry tag, wire enum.
- **Njord.Weather / Njord.Messages**: `SeasonalOutlookHandler`, `MonthlyOutlook`, lifestyle mapping.
- **API budget**: about 30 calls/month per location once the cycle is learned (weight 1), against 300k/month and 10k/day; during Discovery up to about 70 calls/day and location until two consistent update intervals are seen (persisted afterwards).
