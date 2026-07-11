# Proposal: add-kachelmann-ingest

## Why

njord currently has no functional code — only the solution scaffold. Everything
downstream (consensus, MQTT egress, HA discovery) depends on reliably getting
multi-model forecasts out of the Kachelmann API and into a clean domain model.
This change lays that foundation: configuration, domain types, API client, and
the Akka.Streams poll pipeline, ending in a logging sink until the consensus and
egress changes land on top.

## What Changes

- New typed configuration (`NjordOptions`) with:
  - Kachelmann plan presets (`hobby`, `business-starter`, `business-standard`,
    `business-professional`, `business-enterprise`, `custom`) mapping to request
    budgets (requests/month, requests/minute), plus an optional raw budget
    override.
  - Locations list (name, latitude, longitude), model list, poll interval
    (default 60 min). API key comes from env var `Njord__ApiKey` only.
- Startup validation (`IValidateOptions`) that projects monthly usage
  (locations × models × cycles/month) and refuses to start above 80 % of the
  plan budget.
- New weather domain model: `WeatherParameter` (temperature, precipitation,
  windSpeed, windGust, dewpoint, relativeHumidity, cloudCover, pressureMsl),
  `ForecastSeries` (3-hourly points), `ModelForecast` (model + location +
  cycle + series).
- New Kachelmann HTTP client: `GET /forecast/{lat}/{lon}/advanced/3h?model=<id>&units=…`
  with `X-API-Key` header, DTO parsing into the domain model, typed error
  outcomes (auth failure, rate limited, model unavailable, malformed payload).
- New Akka.Streams poll pipeline hosted via Akka.Hosting: restart-with-backoff
  tick source, cycle id per tick, fan-out over locations × models, throttle at
  the per-minute budget, per-cycle aggregation with timeout and quorum, logging
  sink (temporary — replaced by consensus + MQTT in later changes).
- v1 model mix: `ICON-D2`, `ECMWF`, `GFS`, `SWISS1X1` (the "Super HD"
  candidate — its identity must be verified with a probe request before it is
  baked into the default config).

## Capabilities

### New Capabilities

- `service-configuration`: typed options, plan presets and budget override,
  locations/models/interval, startup budget validation.
- `weather-domain`: parameter, series, and per-model forecast types shared by
  ingest and all later changes.
- `kachelmann-client`: authenticated HTTP access to the advanced forecast
  endpoint, DTO-to-domain parsing, typed failure modes.
- `poll-pipeline`: the recurring Akka.Streams flow from tick to aggregated
  per-cycle model forecasts, resilient to individual model failures.

### Modified Capabilities

_None — there are no existing specs yet._

## Impact

- **Code**: `src/Njord/` gains Configuration, Domain, Ingest (client + DTOs),
  and Pipeline areas plus Akka.Hosting bootstrap in `Program.cs`;
  `src/Njord.Tests/` gains the corresponding `*Spec` suites.
- **Dependencies** (via `dotnet add package`, versions in
  `Directory.Packages.props`): Akka.Hosting, Akka.Streams,
  Microsoft.Extensions.Hosting/Options/Http.
- **Runtime**: service now requires `Njord__ApiKey` and at least one configured
  location and model to start.
- **API budget**: default config (1 location × 4 models × hourly cycles)
  ≈ 2,880 requests/month ≈ 14.4 % of the hobby plan's 20,000/month — well under
  the 80 % startup guard (16,000/month). Each additional location adds
  ≈ 2,880/month; the guard trips at 6+ locations on hobby defaults, which is
  the intended behavior.

## Non-goals

- Consensus computation across models (next change).
- MQTT publishing, HA discovery, availability handling (later changes).
- Feels-like/Steadman temperature (needs its own small change on top of the
  domain).
- `model=ALL` usage (response shape undocumented) and weather warnings (no
  endpoint).
- Per-plan model availability probing beyond the single SWISS1X1 verification
  task.
