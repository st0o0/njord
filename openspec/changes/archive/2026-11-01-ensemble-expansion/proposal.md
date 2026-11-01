## Why

The Ensemble Mean API offers 40+ spread variables but njord only requests 3 (`temperature_2m_spread`, `precipitation_spread`, `wind_speed_10m_spread`). This means feature rules have no uncertainty data for most weather parameters. Adding Google WeatherNext 2 as a third model diversifies the comparison (AI vs NWP) and brings `wind_speed_100m`/`wind_direction_100m` which no other ensemble model provides. The `wind_speed_100m`/`wind_direction_100m` variables are also missing from `ForecastParams` entirely.

## What Changes

- Add `google_weathernext2_ensemble_mean` as a third default Ensemble model.
- Expand `EnsembleApiVariable` enum from 3 to ~15 spread variables covering the variables most relevant to existing features.
- Expand `EnsembleParams` and `EnsemblePoint` to carry the new spread values.
- Add `wind_speed_100m` and `wind_direction_100m` to `ForecastApiVariable`/`ForecastParams`/`ForecastPoint`.
- Update Ensemble response parsing to extract the new spread variables.
- Update budget calculations for the third model.

## Non-goals

- Pressure level variables (104 vars -- separate change, needs new feature tier).
- Adding spread consumption to existing feature rules (features already skip missing data; spread can be consumed in a future change).
- Adding ECMWF AIFS or other AI models beyond WeatherNext (future work).
- Daily variables on the Ensemble API.

## Capabilities

### New Capabilities

- `weathernext-model`: Google WeatherNext 2 as third default Ensemble model with its unique variables (`wind_speed_100m`, `wind_direction_100m`).

### Modified Capabilities

- `ensemble-api-options`: Default models expand from 2 to 3; spread variables expand from 3 to ~15.
- `forecast-api-options`: Add `wind_speed_100m` and `wind_direction_100m` to the Forecast variable set.
- `domain-call-config`: Budget calculation updates for 3 models instead of 2.

## Impact

- `src/Njord.OpenMeteo/Ensemble/` -- EnsembleOptions, EnsembleApiVariable, EnsembleClient, response mapping
- `src/Njord.Core/Ensemble/` -- EnsembleParams, EnsemblePoint
- `src/Njord.OpenMeteo/Forecast/` -- ForecastApiVariable, ForecastClient
- `src/Njord.Core/Forecast/` -- ForecastParams, ForecastPoint
- `src/Njord.Polling/Configuration/BudgetGuard.cs` -- 3-model budget estimate
- Tests for all changed files
- `docs/data/features.json` -- document new default models
- Config Builder budget calculation
- CLAUDE.md/AGENTS.md -- update Ensemble defaults documentation

## Budget impact

Adding a third Ensemble model increases Ensemble call weight by ~50% (from 2x to 3x per poll cycle). With 4 forecast days and ~15 spread + weather variables: `ceil(15/10) * (4/14) * 3 models = ceil(2 * 0.286) * 3 = 3` calls per location per poll cycle (was 2). At 60-min intervals, 2 locations: `2 * 24 * 3 = 144 calls/day` (was 96). Monthly: ~4,320 (was ~2,880). Well within the 300k/month free tier. Adding more spread variables increases the per-call weight but stays within budget.
