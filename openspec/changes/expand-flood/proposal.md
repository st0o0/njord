## Why

njord uses the Open-Meteo Flood API (GloFAS) only through its aggregate statistics: `river_discharge`, mean, median, min, max, p25 and p75. The API can also return every ensemble member (`ensemble=true`, 50 members), which gives the real spread of the forecast instead of a handful of summary statistics. The Weather domain already derives a confidence signal from ensemble spread (`ConfidenceTrend`); flood risk has no equivalent, so a single alarming `river_discharge_max` looks the same whether one member or forty reach it. Forecast length is also capped at the default 92 days although the API allows up to 210 (the options validator already accepts 210).

## What Changes

- `FloodClient` / `OpenMeteoRequestBuilder` support the `ensemble=true` request parameter; `FloodDailyData` gains the member series (`river_discharge_member01` to `river_discharge_member50`, exact names verified in task 1.1).
- `FloodParams` (`src/Njord.Forecast/Flood/`) gains an `Ensemble` flag. The statistic flags (`DischargeMean`, `DischargeMedian`, `DischargeMax`, `DischargeMin`, `DischargeP25`, `DischargeP75`) already exist; no further percentile flags are added because the API offers no others (verified in task 1.1).
- `FloodPoint` gains `EnsembleMembers` (`IReadOnlyList<double>`-style immutable list, null when not requested) and derived spread helpers are kept out of the point (pure computation lives in the handler/rule).
- Enhanced `FloodRiskHandler`: when `ensemble=true` data is present, the alert gains a confidence from the share of members exceeding the threshold (same idea as Weather `ConfidenceTrend`); without ensemble data the handler behaves exactly as today.
- New option `FloodOptions.IncludeEnsemble` (default false, `Njord:Flood`), and `FloodOptions.ForecastDays` guidance for values above 92 (default stays 92; the validator range of 1 to 210 is unchanged).
- Config Builder, `docker-compose.example.yml` and `appsettings.Example.json` mirror `IncludeEnsemble`.

## API-budget impact

Flood is one call (weight 1) per location per poll cycle today. Open-Meteo weights a call by variable and time-range volume, and 50 extra member series times up to 210 days is a large request. Task 1.1 reads the Open-Meteo pricing/weight rules and records the effective weight; until verified, the plan assumes the member series raise the call weight above 1. The change handles this explicitly:

- `IncludeEnsemble` is off by default, so default budgets are unchanged.
- `BudgetCalculator` takes a flood weight (1 without ensemble; a verified constant, expected well above 1, with ensemble) so the startup validation against 80 % of 300k/month stays honest. Example with a placeholder weight `W`: 10 locations at 60 min polling is 7.2k x W calls/month for flood, so any `W` above about 20 needs a documented warning.
- Flood data changes once a day (GloFAS daily cycle), so with ensemble on the polling stays at the learned per-domain cycle (discovery) and does not get more frequent.

## Non-goals

- Full hydrological modeling or river routing.
- Historical flood data.
- New alert types (confidence enriches the existing `FloodRisk` alert).
- Exposing all 50 members over gRPC or MQTT (only derived spread/confidence travels on the wire).
- Raising the default `ForecastDays`.

## Capabilities

### New Capabilities

- `flood-expansion`: ensemble request option, ensemble members on the point, ensemble-based confidence in `FloodRisk`.

### Modified Capabilities

None.

## Impact

- **Njord.Forecast** (`src/Njord.Forecast/Flood/`): `FloodParams`, `FloodPoint`.
- **Njord.OpenMeteo** (`src/Njord.OpenMeteo/`): `OpenMeteoRequestBuilder.cs`, `FloodClient.cs`, `FloodResponse.cs`, `Mapping/FloodParamsMappings.cs`, `Mapping/FloodResponseMappings.cs`.
- **Njord.Core** (`src/Njord.Core/Configuration/`): `FloodOptions` (+ validator), `Polling/BudgetCalculator.cs` (flood weight).
- **Njord.Flood**: `FloodRiskHandler`, `FloodRiskAlertRule` (confidence).
- **Njord.Messages / Grpc / Mqtt**: optional confidence member on the flood alert wire (additive).
- **Docs**: Config Builder (`docs/.vitepress/theme/builder`: `budget.ts` mirrors the flood weight), `docker-compose.example.yml`, `src/Njord/appsettings.Example.json`.
