## Why

Air quality data (PM2.5, PM10, ozone, pollen) is one of the most requested weather-adjacent data types for home automation — allergy alerts, exercise advisories, and ventilation recommendations. Open-Meteo provides a dedicated Air Quality API at `air-quality-api.open-meteo.com` with hourly resolution and 5-7 day forecasts. The multi-domain proxy architecture is ready to support it: the Gateway, PollWorker, Pipeline, and gRPC/MQTT consumers all handle DomainKind-based routing.

## What Changes

- Add `FetchAirQualityAsync` to OpenMeteoClient (new endpoint URL, request building, JSON parsing).
- Add `OpenMeteoAirQualityMappings` (response → `DomainForecast<AirQualityPoint>`).
- Add AirQuality route in OpenMeteoGateway (`DomainKind.AirQuality` → fetch + map + offer to pipeline).
- Add `DomainKind.AirQuality` to PollWorker's enabled domains (configurable).
- Add `AirQualityAlertProjector` + enrichments: PollenAlert (high pollen count), Pm25Alert (poor air quality), ExerciseAdvisory (combined weather + AQ).
- Register AirQuality ProjectionGroup in DI.
- Update gRPC proto: AirQuality domain data message populated.
- Update MQTT: `njord/{loc}/air-quality/{param}` topics with real data.
- API budget: +1 call per location per cycle (air-quality-api is a separate endpoint).

## Non-goals

- Historical air quality data.
- Custom AQ model selection (auto-select is fine).

## Capabilities

### New Capabilities

- `air-quality-fetch`: OpenMeteoClient Air Quality endpoint, parsing, mapping.
- `air-quality-enrichment`: Pollen and PM2.5 alerts, exercise advisory as ProjectionGroups.

### Modified Capabilities

- `multi-domain-polling`: PollWorker enables AirQuality domain.
- `open-meteo-multi-endpoint`: Gateway routes AirQuality.

## Impact

- **Njord.OpenMeteo**: New client method, DTOs, mappings.
- **Njord.Alerts** (or new Njord.AirQuality.Alerts): New alert rules.
- **Njord.Pipeline**: PollWorker enabled domains extended.
- **API budget**: +1 call/cycle/location (~720/month for 1 location at 60min interval).
