# Replace Kachelmann with Open-Meteo

## Why

Kachelmann requires a paid plan and an API key that has so far blocked real-world
verification (two key-dependent probes are still open from the previous change).
Open-Meteo delivers the same per-model forecasts (ICON, ECMWF, GFS, UKMO, …) free
for non-commercial use, without any API key, with 15× the monthly request budget,
hourly resolution over the full range (up to 16 days), and a built-in per-model
apparent temperature — all verified against the live API on 2026-07-11. With MQTT
egress not yet built and no production deployment, ingest is the only affected
zone; this is the cheapest moment the switch will ever be.

## What Changes

- **BREAKING**: Replace the Kachelmann HTTP client with an Open-Meteo client
  (`GET https://api.open-meteo.com/v1/forecast`, no authentication). Configured
  model ids change from Kachelmann ids (`ICON-D2`, `ECMWF`, …) to Open-Meteo ids
  (`icon_d2`, `ecmwf_ifs025`, …).
- Remove API-key handling entirely: the `Njord__ApiKey` environment variable, the
  startup guard for a missing key, and the key-leak protection requirements.
- Replace the Kachelmann plan presets (`Hobby`, `BusinessStarter`, …) with a
  single Open-Meteo free-tier budget default (300,000 requests/month,
  10,000/day, 600/minute — soft limits, verified); the explicit budget override
  stays.
- Adjust the client failure taxonomy: `AuthFailed` is removed; an unknown model
  id or a model outside its geographic coverage returns HTTP 400 with an error
  payload (verified) and maps to `ModelUnavailable`.
- Add `apparentTemperature` (°C) to the closed v1 parameter set — the API
  delivers it per model; the plan to compute Steadman ourselves is obsolete.
- New default model list, all ids verified against the live API for the shipped
  default location (Lucerne, 47.05/8.31): `icon_d2`, `icon_eu`, `icon_global`,
  `ecmwf_ifs025`, `gfs_seamless`, `ukmo_global_deterministic_10km`,
  `meteoswiss_icon_ch1`, `meteoswiss_icon_ch2` (the ~1 km "Super HD" class the
  old `SWISS1X1` hunch was after — now documented and verified).
- Per-model fan-out is retained: one request per (location, model) pair. The
  poll-pipeline shape (tick → fan-out → throttle → aggregate) is unchanged.
- Update CLAUDE.md: replace the verified-Kachelmann-facts section with the
  verified Open-Meteo facts.

## Capabilities

### New Capabilities

- `openmeteo-client`: HTTP client for the Open-Meteo forecast API — per
  (location, model) fetch with metric units (`wind_speed_unit=ms`), mapping of
  the flat arrays payload to `ModelForecast`, typed failure outcomes without
  retries, tolerance for model-specific horizon limits (trailing `null`s).

### Modified Capabilities

- `service-configuration`: Kachelmann plan presets and the API-key requirement
  are removed; the resolved request budget defaults to the Open-Meteo free tier
  with an optional explicit override; startup projection guard stays.
- `weather-domain`: the closed v1 parameter set gains `apparentTemperature`
  (°C); model-id examples in the requirement text change from Kachelmann to
  Open-Meteo ids (the value object stays a free-form wrapped string).

### Removed Capabilities

- `kachelmann-client`: fully replaced by `openmeteo-client`.

Note: `poll-pipeline` is intentionally **not** modified — fan-out, per-minute
throttling, aggregation window, and supervision are all expressed against the
resolved budget and are provider-agnostic.

## Impact

- **Code**: `src/Njord/Ingest/*` (client, DTOs, JSON source-gen context, DI
  registration), `src/Njord/Configuration/` (`NjordPlan` and `PlanBudgets`
  removed, `NjordOptions` + validator simplified), `src/Njord/Domain/WeatherParameter.cs`
  (new parameter), `src/Njord/appsettings.json` (base URL, default models),
  `src/Njord/Program.cs`. Tests mirror all of it; the env-gated smoke test loses
  its key requirement and can run unconditionally (network-gated only).
- **Dependencies**: none added or removed (HttpClient + System.Text.Json
  source generation stay).
- **Docs**: CLAUDE.md API-facts section and decision notes (Steadman, plan
  presets).
- **API budget estimate** (Open-Meteo free tier, soft limits): defaults of
  1 location × 8 models × 24 cycles/day ≈ **5,760 requests/month ≈ 1.9 %** of
  300,000/month (192/day vs. 10,000/day). A generous 5-location setup at the
  same cadence ≈ 28,800/month ≈ 9.6 %. Per-cycle burst of 8–40 requests vs.
  600/minute. Headroom is no longer a design constraint.

## Non-goals

- No provider abstraction (`IWeatherProvider`) and no Kachelmann fallback — the
  ingest zone boundary is the abstraction.
- No multi-model batching per request (possible with Open-Meteo, verified, but
  per-model calls keep failure isolation and the pipeline unchanged; call
  weighting makes the cost roughly equal).
- No 15-minutely data, no ensemble/historical/air-quality endpoints.
- No support for Open-Meteo paid tiers (customer API key endpoints).
- Consensus computation and MQTT egress remain separate future changes.
