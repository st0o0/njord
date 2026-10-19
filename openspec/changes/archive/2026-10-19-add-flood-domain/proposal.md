## Why

Flood forecasting with river discharge data adds significant value for locations near rivers. Open-Meteo provides a Flood API at `flood-api.open-meteo.com` with daily river discharge forecasts up to 210 days ahead, including percentile data (p25/p75) and historical medians. The GloFAS model automatically finds the largest river within 5km of the location.

## What Changes

- Add `FetchFloodAsync` to OpenMeteoClient (new endpoint URL, daily resolution, JSON parsing).
- Add `OpenMeteoFloodMappings` (response → `DomainForecast<FloodPoint>` with Daily resolution).
- Add Flood route in OpenMeteoGateway (`DomainKind.Flood` → fetch + map + offer to pipeline).
- Add `DomainKind.Flood` to PollWorker's enabled domains (configurable).
- Add `FloodRiskProjector` + `FloodRiskAlert` enrichment: discharge vs percentiles → risk assessment.
- Add `FloodRainProjector` (cross-domain: Flood × Weather) + `FloodRainAlert`: heavy rain upstream + rising discharge.
- Register Flood ProjectionGroups in DI.
- Update gRPC proto: Flood domain data message populated.
- Update MQTT: `njord/{loc}/flood/{param}` topics.
- API budget: +1 call per location per cycle.

## Non-goals

- Historical flood analysis.
- Custom river selection (auto-select by proximity is fine).

## Capabilities

### New Capabilities

- `flood-fetch`: OpenMeteoClient Flood endpoint, parsing, mapping.
- `flood-enrichment`: Flood risk alert and cross-domain flood-rain correlation.

### Modified Capabilities

- `multi-domain-polling`: PollWorker enables Flood domain.
- `open-meteo-multi-endpoint`: Gateway routes Flood.

## Impact

- **Njord.OpenMeteo**: New client method, DTOs, mappings.
- **Njord.Alerts** or new project: Flood risk and cross-domain alerts.
- **Njord.Pipeline**: PollWorker enabled domains extended.
- **API budget**: +1 call/cycle/location.
