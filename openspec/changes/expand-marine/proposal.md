## Why

njord requests only the combined wave values (`wave_height`, `wave_period`, `wave_direction`) and sea surface temperature from the Open-Meteo Marine API. The API separates wind waves (local, short-period chop) from swell (distant, long-period, organized energy). For surfing, sailing and kayaking that distinction is the whole story: a 1.5 m sea of 5 s wind waves and a 1.5 m swell of 12 s are different days. The same endpoint also serves `sea_level_height_msl` for tidal information.

## What Changes

- `MarineParams` (`src/Njord.Forecast/Marine/`) gains flags: `WindWaveHeight`, `WindWavePeriod`, `WindWaveDirection`, `WindWavePeakPeriod`, `SwellWaveHeight`, `SwellWavePeriod`, `SwellWaveDirection`, `SwellWavePeakPeriod`, `SeaLevelHeight`.
- `MarinePoint` gains nullable members for each. The existing `WaveHeight`/`WavePeriod`/`WaveDirection` keep their meaning and are documented as the combined (total) sea state; no rename, no wire break.
- `MarineHourlyData` (`src/Njord.OpenMeteo/MarineResponse.cs`) gains the matching JSON properties; `MarineParamsMappings` and `MarineResponseMappings` gain one arm per member.
- New enrichment `SurfConditions` (`IDomainHandler` in `src/Njord.Marine/`), off by default. It scores swell quality (swell period, swell height band, and wind direction relative to swell direction as offshore/cross/onshore when Weather wind is available) and emits a lifestyle event over the existing `LifestyleUpdate` wire (`ToLifestyleUpdate()`), so no new proto message or topic is needed.
- `SwimmingConditionsHandler` is not changed; it keeps using combined wave height (design D4).
- Wire: new members on the Marine payloads are additive and nullable (gRPC and MQTT).
- Config Builder, `docker-compose.example.yml` and `appsettings.Example.json` mirror the `SurfConditions` toggle.

## API-budget impact

None. Marine is one call (weight 1) per location per poll cycle regardless of variable count, so `BudgetCalculator` output is unchanged (for example 10 locations at a 60 min interval stay at about 7.2k calls/month for the marine share, no increase). Task 1.1 re-verifies that the marine endpoint has no variable-count weighting. The extra variables are requested only when `SurfConditions` is enabled (optional flags otherwise stay unrequested), keeping default payloads as small as today.

## Non-goals

- Tide prediction (only the raw `sea_level_height_msl` series is exposed).
- Historical marine data.
- Spot-specific surf models (bathymetry, break type, local wind shadowing).
- Changing `SwimmingConditionsHandler` behavior.
- Renaming the existing combined wave members.

## Capabilities

### New Capabilities

- `marine-expansion`: wind-wave and swell variables, sea level height, `SurfConditions` enrichment.

### Modified Capabilities

- `enrichment-config`: `SurfConditions` toggle added.

## Impact

- **Njord.Forecast** (`src/Njord.Forecast/Marine/`): `MarineParams`, `MarinePoint`.
- **Njord.OpenMeteo** (`src/Njord.OpenMeteo/`): `MarineResponse.cs`, `Mapping/MarineParamsMappings.cs`, `Mapping/MarineResponseMappings.cs`.
- **Njord.Core** (`src/Njord.Core/Configuration/EnrichmentOptions.cs`); **Njord.Messages** (`Pipeline/` lifestyle event + `LifestyleMappings`, `LifestyleKind` member).
- **Njord.Marine**: `SurfConditionsHandler`, `SurfConditionsRule` (pure scoring), registration in `ServiceCollectionExtensions`.
- **Njord.Grpc / Njord.Mqtt**: nullable Marine members; lifestyle kind coverage.
- **Docs**: Config Builder (`docs/.vitepress/theme/builder`, `docs/data/enrichment.json`), `docker-compose.example.yml`, `src/Njord/appsettings.Example.json`.
