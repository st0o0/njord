## Why

The v3 entity stream now correctly routes Observation results (observation-entity-routing fix), but only 4 sample observations exist. HA users want standalone sensor entities for common domain values — air quality pollutants, marine conditions, forecast variables not covered by WeatherResult. Now is the time because the routing infrastructure is ready.

## What Changes

- Add 21 new Observation features across 4 domains, each a trivial FeatureRule reading one field
- Register all in `ServiceCollectionExtensions.AddFeatures`
- Add entries to `docs/data/features.json`
- All disabled by default

### AirQuality (6)
Pm10, Ozone, NitrogenDioxide, EuropeanAqi, CarbonMonoxide, SulphurDioxide

### Marine (4)
WaterTemperature, WavePeriod, WaveDirection, OceanCurrentVelocity

### Forecast (8)
DewPoint, WindGusts, SoilTemperature, SoilMoisture, SnowDepth, Evapotranspiration, ShortwaveRadiation, Visibility

### Flood (1)
DischargeMean

## Non-goals

- Pollen observations (already dedicated alert/trend features)
- Sub-AQI breakdowns (EuropeanAqiPm25 etc. — too niche)
- Height-level winds (80m/100m/120m — wind farm use case)
- Ensemble/Seasonal observations (not point-in-time values)
- WeatherResult fields (already exposed as the weather entity per location)

## API budget impact

Zero. All fields come from domains already fetched for existing features. The observation features add no new API variables to `DomainRequirements` — they use Optional flags or fields already required by other features. No new domains, no new calls.

## Capabilities

### New Capabilities

None — this is an expansion of an existing pattern, not a new capability.

### Modified Capabilities

- `observation-features`: Add requirement that the system provides observations for commonly useful domain sensor values beyond the initial sample set

## Impact

- `Njord.Features`: 21 new rule + config file pairs in `Observations/<Name>/`
- `Njord.Features`: Registration in `ServiceCollectionExtensions.AddFeatures`
- `Njord.Features.Tests`: 21 new test classes
- `docs/data/features.json`: 21 new entries
- No changes to Messages, Grpc, Pipeline, or any other project
