## Why

The Satellite domain fetches historical observation data from `satellite-api.open-meteo.com/v1/archive` at ~720 calls/month/location for marginal value. SolarYield already marks Satellite as optional and degrades gracefully to forecast-only via `SolarIrradianceBlend`. SatelliteRadiation is a niche feature reporting observed irradiance most HA users will not act on -- automations care about upcoming conditions, not past observations. The 3-hour staleness cutoff in the blend means satellite data is frequently discarded when archive latency exceeds that window.

## What Changes

- **BREAKING**: Remove `DomainKind.Satellite` and the entire Satellite fetch pipeline (client, gateway dispatch, polling, budget projection, persistence records, gRPC mapping).
- **BREAKING**: Remove `SatelliteRadiation` feature and `SolarIrradianceBlend`.
- **BREAKING**: Remove `SatelliteParams` flag enum and `SatelliteRadiationPoint` (Core/Solar types).
- **BREAKING**: Remove `Njord:Satellite` config section (PastHours). Rename `SatelliteOptions` to `SolarOptions` retaining only `Tilt` and `Azimuth` under `Njord:Solar`, because ForecastClient and EnsembleClient still need panel orientation for GTI requests.
- Simplify `SolarYield` to use forecast GTI directly, removing the blend.
- Update `features.json`, Config Builder, `docker-compose.example.yml`, and `appsettings.Example.json`.

## Capabilities

### New Capabilities

- `solar-options`: Panel orientation config (`Tilt`, `Azimuth`) moves from `Njord:Satellite` to `Njord:Solar` as `SolarOptions`, used by Forecast/Ensemble clients for GTI.

### Modified Capabilities

- `satellite-radiation`: Removed entirely (spec becomes archived/superseded).

## Impact

- **Budget**: Saves ~720 calls/month/location (2.4% of free tier per location).
- **Code**: Removes `Njord.Core/Solar/` types, `Njord.OpenMeteo/Satellite/` (client, options, validator, mapping), satellite dispatch in gateway, `SolarIrradianceBlend` in Features, `SatelliteRadiation` feature, satellite entries in `DomainKind`, `DomainCallConfig`, `BudgetGuard`, `DomainRequirements`, persistence records, gRPC mapping and proto fields.
- **Config**: `Njord:Satellite:Tilt`/`Azimuth` moves to `Njord:Solar:Tilt`/`Azimuth`. `Njord:Satellite:PastHours` removed. `Njord:Features:SatelliteRadiation` removed.
- **Wire**: `DOMAIN_SATELLITE`, `SatelliteSample` removed from proto. **BREAKING** for ha-njord consumers.
- **ha-njord**: Must remove satellite entity handling (co-design required).

## Non-goals

- Removing SolarYield itself -- it works well with forecast-only GTI.
- Changing any other domain's polling or discovery mechanism.
- Adding the Metadata API for polling optimization (explored and deferred).

## API-budget impact

Removes ~720 calls/month/location. For 3 locations that frees ~2160 calls/month (0.7% of the 300k budget). No new polling added.
