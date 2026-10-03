## Why

The consensus MQTT payload and gRPC output have data quality issues that mislead consumers:

1. **Metadata is per-first-parameter, not per-parameter**: `_spread`, `_agreement`, and `_models_used` in the MQTT JSON come from the first parameter in the result (typically `temperature_2m`), not from the parameter whose value the consumer is looking at. A consumer seeing `precipitation_probability: 12.5, _models_used: 4` may think 4 models contributed to the precipitation probability, when in reality only 1 did (the other 3 don't have that parameter).

2. **Daily forecast entries with all-null values are published**: When a model's horizon doesn't fully cover a calendar day, the Open-Meteo API returns null values for that day's daily aggregates. njord currently filters all-null entries (fixed earlier this session), but the daily-filtering spec and its interaction with `EffectiveForecastDays` (ceiling-based) is undocumented.

3. **Model coverage registry was outdated**: Fixed earlier this session with live API probe data (now in `tools/model-probes/`).

This change focuses on making the consensus output trustworthy: per-parameter metadata so consumers know exactly how many models contributed to each value.

## What Changes

- Restructure the consensus MQTT payload to include `_models_used` per parameter instead of globally per horizon.
- Update the gRPC `HorizonConsensus` proto to carry per-parameter model counts (or document that `available_models` is per-parameter already via the `ParameterConsensus` → `HorizonConsensus` nesting).
- Add a spec for the daily forecast filtering behaviour.
- Commit the model coverage registry fixes and daily-filter fix from this session.

## Non-goals

- Blacklisting specific models that have parameter gaps (e.g., `arome_france_hd` missing `cloud_cover`). Model selection is a user configuration choice.
- Changing which parameters are requested from the API.
- No API-budget impact.

## Capabilities

### New Capabilities

- `daily-forecast-filtering`: Spec documenting the behaviour of filtering all-null daily entries and the `EffectiveForecastDays` ceiling logic.

### Modified Capabilities

- `consensus-computation`: Consensus MQTT payload carries per-parameter model counts instead of a single global `_models_used`.

## Impact

- Modified: `src/Njord/Mqtt/StatePayloadBuilder.cs` — restructure consensus JSON payload.
- Modified: `src/Njord/Configuration/ModelCoverageRegistry.cs` — already fixed, needs commit.
- Modified: `src/Njord/Ingest/OpenMeteoClient.cs` — daily filter already fixed, needs commit.
- Modified: `src/Njord.Tests/Ingest/OpenMeteoClientSpec.cs` — test already updated.
- Modified: `src/Njord.Tests/Egress/ModelStateActorSpec.cs` — test already updated.
- New spec for daily forecast filtering.
- Delta spec for consensus computation.
