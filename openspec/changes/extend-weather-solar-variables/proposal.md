## Why

Solar radiation data (shortwave, direct, diffuse radiation, sunshine duration) is part of the standard Open-Meteo forecast/ensemble API — no separate endpoint needed. Adding these variables to the existing Ensemble request gives PV yield estimation and solar exposure data at zero additional API cost. The WeatherPoint already has optional fields for some of these; this change wires them up.

## What Changes

- Extend the Ensemble request URL to include solar variables: `shortwave_radiation`, `direct_radiation`, `diffuse_radiation`, `direct_normal_irradiance`, `sunshine_duration`.
- Extend `WeatherPoint` with solar fields if not already present (DirectRadiation, DiffuseRadiation, ShortwaveRadiation, SunshineDuration, DirectNormalIrradiance).
- Update `OpenMeteoEnsembleMappings` to parse and map solar columns from the response.
- Add optional `SolarYieldProjector` + `SolarYieldEnrichment`: estimated daily PV yield based on radiation data and optional panel configuration (azimuth, tilt, kWp from config).
- No new API call, no new DomainKind needed — solar data lives on the Weather domain.
- API budget: zero impact (same request, additional variables don't cost extra below 10 hourly variables).

## Non-goals

- Separate Solar domain or endpoint.
- Satellite observation data (that would be a different API).

## Capabilities

### New Capabilities

- `solar-variables`: Solar radiation variables in the Ensemble request.
- `solar-yield-enrichment`: PV yield estimation as a ProjectionGroup.

### Modified Capabilities

- `ensemble-forecast`: Request includes solar variables, WeatherPoint carries them.

## Impact

- **Njord.OpenMeteo**: Request URL extended, mapping extended.
- **Njord.Model**: WeatherPoint fields (may already exist from core-domain-model).
- **Njord.Pipeline** or new enrichment: SolarYield ProjectionGroup.
- **API budget**: Zero — same call weight.
