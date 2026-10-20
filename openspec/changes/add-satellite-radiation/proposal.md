## Why

The Ensemble API provides modeled solar radiation forecasts, but satellite-observed radiation is significantly more accurate for recent and current hours. The Open-Meteo Satellite Radiation API provides measured solar irradiance from geostationary satellites: GHI, direct, diffuse, DNI and GTI (Global Tilted Irradiance), which accounts for panel tilt and azimuth. For PV owners using `SolarYield`, this is the difference between a model guess and the measured production potential of the last hours.

## What Changes

- New `ISatelliteRadiationClient` + `SatelliteRadiationClient` in `src/Njord.OpenMeteo/` - thin typed client for `satellite-radiation-api.open-meteo.com/v1/radiation`, built with `OpenMeteoRequestBuilder`; tilt and azimuth are passed in by the caller.
- New `SatelliteRadiationResponse` DTO registered in `OpenMeteoJsonContext`.
- New `SatelliteRadiationService` (`BackgroundService`) in `src/Njord.OpenMeteo/` - fetches hourly per location (configurable interval) while the toggle is enabled and stores the recent satellite data. Standalone: not an actor, not a `DomainKind`, not routed through the Gateway.
- New `ISatelliteRadiationStore` + in-memory `SatelliteRadiationStore` in `src/Njord.Core/Domain/`.
- New `SatelliteRadiationPoint` in `src/Njord.Forecast/Solar/` with `GHI`, `DirectRadiation`, `DiffuseRadiation`, `DNI`, `GTI` (all `double?`).
- Enhanced `SolarYieldHandler`: uses satellite data for past/current hours and the ensemble forecast for future hours, blended at the transition point. Without satellite data the handler behaves exactly as today.
- New `EnrichmentOptions.SatelliteRadiation` toggle (default false, it adds API calls); mirrored in the Config Builder and `docker-compose.example.yml`.
- `SolarYieldOptions.Tilt` / `Azimuth` are passed to the satellite API for GTI; no new options for orientation.
- gRPC + MQTT: satellite-based data is surfaced through the existing Solar/Lifestyle wire (`LifestyleMappings.ToLifestyleUpdate()`), no new proto message.

## Relation to ensemble GTI

The Ensemble API also offers Global Tilted Irradiance (with tilt/azimuth request parameters). Satellite radiation therefore complements rather than competes with the ensemble: the ensemble remains the only source for future hours (the satellite API only observes the past and nowcast), while the satellite API supplies measured values for recent hours. Whether satellite data could eventually replace ensemble solar data for the observed window, and whether ensemble GTI should replace the heuristic `OrientationFactor` in `SolarYieldHandler`, is evaluated in design D6 and left as a follow-up; this change does not remove any ensemble variable.

## Non-goals

- Historical radiation analysis (past days or weeks).
- Replacing the Ensemble radiation forecast entirely (it stays the source for future hours).
- Real-time (sub-hourly) updates.
- Making `Solar` a polled `DomainKind` or routing satellite data through the Gateway/PollWorker.
- Switching `SolarYieldHandler` to ensemble GTI (separate follow-up).

## Capabilities

### New Capabilities

- `satellite-radiation`: ISatelliteRadiationClient, SatelliteRadiationService, ISatelliteRadiationStore, SatelliteRadiationPoint, satellite/ensemble blend in SolarYield.

### Modified Capabilities

- `enrichment-config`: `SatelliteRadiation` toggle added.

## Impact

- **Njord.OpenMeteo** (`src/Njord.OpenMeteo/`): client, response DTO, `SatelliteRadiationService`, `Mapping/SatelliteRadiationResponseMappings.cs`; registration in `Configuration/OpenMeteoSetupContainer.cs`.
- **Njord.Core** (`src/Njord.Core/`): `Domain/ISatelliteRadiationStore.cs`, `Configuration/EnrichmentOptions.cs`, a small interval options class (design D2).
- **Njord.Forecast** (`src/Njord.Forecast/Solar/`): `SatelliteRadiationPoint`.
- **Njord.Weather** (`src/Njord.Weather/Lifestyle/SolarYieldHandler.cs`): store dependency and blend.
- **Njord.Messages / Grpc / Mqtt**: mapping coverage only.
- **Docs**: Config Builder (`docs/.vitepress/theme/builder`, `docs/data/enrichment.json`), `docker-compose.example.yml`, `appsettings.Example.json`.
- **API budget**: 1 call per location per hour when enabled, about 720 calls/month per location (about 24/day), against 300k/month and 10k/day. Ten locations is about 7.2k calls/month (2.4 %). Weight 1 per call. Off by default. Not a polled domain, so `BudgetCalculator` does not count it; it is covered by the 80 % startup margin and task 2.5 checks whether the budget model should include it when the toggle is on. Politeness: one request per location per interval, jittered first run, 15 min retry on failure, no burst at startup.
