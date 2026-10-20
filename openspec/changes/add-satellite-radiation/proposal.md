## Why

The Ensemble API provides modeled solar radiation forecasts, but satellite-observed radiation is significantly more accurate for recent and current hours. The Open-Meteo Satellite Radiation API provides measured solar irradiance from geostationary satellites: GHI, direct, diffuse, DNI and GTI (Global Tilted Irradiance), which accounts for panel tilt and azimuth. For PV owners using `SolarYield`, this is the difference between a model guess and the measured production potential of the last hours.

## What Changes

- New `DomainKind.Satellite` with `SatelliteRadiationPoint` (`GHI`, `DirectRadiation`, `DiffuseRadiation`, `DNI`, `GTI`, all `double?`) and the `[Flags] SatelliteParams` enum in `src/Njord.Forecast/Solar/`. Same pattern as Weather, AirQuality, Flood, Marine and Seasonal: the gateway fetches it, the PollWorker learns its roughly hourly update cycle through Discovery, the pipeline carries it as `SatelliteDataReceived`.
- New `ISatelliteRadiationClient` + `SatelliteRadiationClient` in `src/Njord.OpenMeteo/` - thin typed client for `satellite-api.open-meteo.com/v1/archive` (see design D1), built with `OpenMeteoRequestBuilder`; tilt and azimuth come from `SolarYieldOptions`.
- New `SatelliteOptions` (`Njord:Satellite:PastHours`, default 6, 1..48).
- New feature `FeatureKind.SatelliteRadiation` (config key `SatelliteRadiation`, category Energy, off by default, about 720 calls/month/location) with `SatelliteRadiationHandler`, which emits `SatelliteRadiationComputed` (newest observed GTI/GHI and the blended next-24 h irradiation).
- `SolarIrradianceBlend` (Njord.Weather): satellite GTI for hours up to the newest observation, ensemble GTI for later hours; satellite data older than 3 h is ignored. `SolarYieldHandler` uses it, so without satellite data it behaves exactly as before.
- gRPC: `DOMAIN_SATELLITE = 7`, `SatelliteData`/`SatelliteSample` in the domain data oneof. The computed event reaches gRPC and MQTT through the existing lifestyle wire (`LifestyleKind.SatelliteRadiation`, `satellite_radiation`).
- Config Builder: `Satellite` domain and the `SatelliteRadiation` feature in `docs/data/features.json`; `docker-compose.example.yml`, configuration docs, `AGENTS.md`.

## Relation to ensemble GTI

`SolarYield` already reads the ensemble `global_tilted_irradiance`, so both sources are GTI. The ensemble stays the only source for future hours; the satellite supplies measured values for the observed hours. No ensemble variable is removed.

## Non-goals

- Historical radiation analysis (past days or weeks).
- Replacing the ensemble radiation forecast.
- A background service, store or out-of-band fetching: the satellite data is a normal polled domain.
- Smoothing at the seam between observed and forecast hours.

## Capabilities

### New Capabilities

- `satellite-radiation`: Satellite domain, client, feature, handler and the satellite/ensemble blend.

## Impact

- **Njord.Forecast**: `DomainKind.Satellite`, `Solar/SatelliteRadiationPoint.cs`, `Solar/SatelliteParams.cs`.
- **Njord.Core**: `DomainRequirements` (`Satellite`/`OptionalSatellite`), `LocationForecast.Satellite`, `FeatureKind.SatelliteRadiation`, `SatelliteOptions` + validator, `BudgetCalculator` counts the domain per poll cycle.
- **Njord.OpenMeteo**: client, response DTO, `SatelliteParamsMappings`, `SatelliteRadiationResponseMappings`, gateway dispatch, registration.
- **Njord.Pipeline / Polling**: `SatelliteDataReceived` mapping, telemetry tag.
- **Njord.Weather**: `SolarIrradianceBlend`, `SatelliteRadiationHandler`, `SolarYieldHandler`.
- **Njord.Messages / Grpc / protos**: `SatelliteRadiationComputed`, lifestyle mapping, proto domain and sample.
- **API budget**: with the default 60 min poll interval one call per location and hour, about 720 calls/month (about 24/day), weight 1, against 300k/month and 10k/day. Ten locations is about 7.2k calls/month (2.4 %). It is a normal domain, so `BudgetCalculator` and the 80 % startup guard already count it. Off by default.
