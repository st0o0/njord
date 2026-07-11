# Design — replace-kachelmann-with-openmeteo

## Context

The ingest zone currently talks to the Kachelmann API (`KachelmannClient`,
DTOs, JSON source-gen context, DI registration in `src/Njord/Ingest/`), gated
by an API key and plan-based request budgets (`NjordPlan`/`PlanBudgets` in
`src/Njord/Configuration/`). Domain and pipeline are provider-agnostic by
design (three-zone guardrail); MQTT egress does not exist yet. The service has
never run against the real API — the key never materialized.

All Open-Meteo facts below were verified against the live API on 2026-07-11
with probe requests (no key needed):

- Endpoint `GET https://api.open-meteo.com/v1/forecast?latitude=&longitude=`,
  no authentication; free tier is non-commercial, soft limits 600/min,
  5,000/h, 10,000/day, 300,000/month. Calls are weighted: >10 hourly
  variables or >2 weeks of data count fractionally as multiple calls.
- Single-model requests (`&models=icon_d2`) return flat arrays under `hourly`
  with **unsuffixed** variable names plus a `hourly_units` object. Multi-model
  requests suffix every variable with the model id.
- Wind defaults to **km/h**; `wind_speed_unit=ms` switches to m/s.
- `timeformat=unixtime` returns epoch seconds (default is naive ISO 8601
  strings without offset).
- Model ids verified working: `icon_d2`, `icon_eu`, `icon_global`,
  `ecmwf_ifs025`, `gfs_seamless`, `ukmo_global_deterministic_10km`,
  `meteoswiss_icon_ch1`, `meteoswiss_icon_ch2`.
- A single requested model outside its geographic coverage → HTTP 400 with
  `{"error":true,"reason":"No data is available for this location"}`.
  An invalid model id → HTTP 400 with `{"error":true,"reason":"…invalid
  String value …"}`. Values beyond a model's forecast horizon are `null`
  entries in otherwise valid arrays.
- `apparent_temperature` is available as an hourly variable per model.

## Goals / Non-Goals

**Goals:**

- Swap the ingest zone to Open-Meteo with zero requirement changes to
  `poll-pipeline` and minimal, explicit changes to `weather-domain` and
  `service-configuration`.
- Remove all API-key machinery.
- Keep every architecture guardrail intact (zones, streams/actors split,
  static entity set, no `Zip`, `TimeProvider`).

**Non-Goals:**

- Provider abstraction or Kachelmann fallback (the ingest zone boundary is the
  abstraction).
- Multi-model batched requests, 15-minutely data, ensemble/historical
  endpoints, paid-tier (customer API key) support.
- Consensus and MQTT egress (separate changes).

## Decisions

### D1: Keep per-model fan-out (one request per location × model)

Open-Meteo can serve all models in one request per location (verified,
variables come back suffixed with the model id). Rejected anyway:

- Call weighting makes N single-model calls ≈ one N-model call in budget
  terms, so batching buys nothing where budget no longer matters anyway.
- Per-model requests keep failure isolation: one 400/timeout affects one
  (location, model) pair, and the existing aggregation/quorum semantics stay
  meaningful.
- The pipeline shape (tick → fan-out → throttle → HTTP → aggregate) survives
  the provider swap untouched.

### D2: Request shape

`GET /v1/forecast?latitude={lat}&longitude={lon}&models={id}`
`&hourly=temperature_2m,apparent_temperature,precipitation,wind_speed_10m,wind_gusts_10m,dew_point_2m,relative_humidity_2m,cloud_cover,pressure_msl`
`&wind_speed_unit=ms&timeformat=unixtime&forecast_days=4`

- Exactly 9 hourly variables → call weight stays 1.0 (≤10-variable threshold).
- `forecast_days=4` yields 96 h hourly, covering the +72 h horizon requirement
  with margin; ≤2 weeks keeps weight at 1.0.
- `timeformat=unixtime` over default naive ISO strings: epoch seconds map
  directly to `DateTimeOffset.FromUnixTimeSeconds` — no timezone ambiguity.
  Alternative (parse naive strings as UTC) rejected as a silent-bug magnet.
- `wind_speed_unit=ms` because the domain unit is m/s and the API default is
  km/h. The client asserts the returned `hourly_units` match expectations and
  reports `MalformedPayload` otherwise, so a silent API default change cannot
  corrupt values.

### D3: Failure taxonomy shrinks by exactly one reason

`FetchOutcome` keeps `Success`, `RateLimited` (429), `ModelUnavailable`,
`MalformedPayload`, `Transport`. `AuthFailed` is deleted — there is no auth.
HTTP 400 with an `{"error":true,"reason":…}` body maps to `ModelUnavailable`
carrying the reason (covers both invalid ids and out-of-coverage locations —
the API does not distinguish them usefully, and both mean "this (location,
model) pair yields nothing this cycle"). No retries, as before.

### D4: Trailing nulls become points with missing values

Beyond a model's horizon (e.g. `icon_d2` ends near +48 h) the API returns
`null` array entries. The client maps them onto the existing domain tolerance:
a `ForecastPoint` with absent parameter values, never a dropped point, never an
error. Points where **all** parameters are null MAY be trimmed from the tail.
This feeds the existing guardrail — missing value = `unavailable` state, and
far horizons simply have fewer models in consensus later.

### D5: Configuration — plans die, the budget stays

`NjordPlan` and `PlanBudgets` are deleted. `RequestBudget`
(requests/month + requests/minute) survives with new defaults: 300,000/month
and 600/min (Open-Meteo free tier). The optional explicit override stays for
self-throttling. The 80 % startup projection guard stays unchanged — it now
guards a soft limit instead of a hard one, which also keeps us polite toward a
free service. The 10,000/day soft limit needs no own dimension: uniform
interval polling that satisfies 300k/month satisfies 10k/day (300k/30 = 10k).
`Njord__ApiKey` handling, validation, and key-leak rules are deleted.

### D6: Naming — provider-honest, no speculative neutrality

`IKachelmannClient` → `IOpenMeteoClient`, `KachelmannClient` →
`OpenMeteoClient`, DTOs/JsonContext renamed accordingly. The interface exists
for testability, not provider abstraction, so it carries the provider's name.
`WeatherModel` stays a free-form wrapped string; only examples/defaults change
to Open-Meteo ids. The snake_case ids are MQTT-topic-friendly for the later
egress change.

### D7: Default model list (verified for the shipped default location)

The shipped `appsettings.json` location is Lucerne (47.05/8.31) — the old
default list carried `SWISS1X1` for a reason. All eight candidates verified
against the live API for exactly that coordinate: `icon_d2`, `icon_eu`,
`icon_global`, `ecmwf_ifs025`, `gfs_seamless`,
`ukmo_global_deterministic_10km`, `meteoswiss_icon_ch1`,
`meteoswiss_icon_ch2`. The meteoswiss pair (~1 km, ICON-CH1/CH2) is the
"Super HD" class the `SWISS1X1` hunch was after — coverage-limited to the
Alps region, which the default location is inside; users elsewhere drop them
per config (out-of-coverage yields `ModelUnavailable` per cycle, visible in
the summary log). The two open Kachelmann probes (SWISS1X1, key smoke test)
are hereby obsolete.

### D8: Smoke test becomes network-gated instead of key-gated

The env-gated smoke spec keeps its opt-in gate (it still needs network), but
drops the API-key requirement — anyone can run it anytime.

## Risks / Trade-offs

- [Free tier has soft limits and no SLA] → Budget guard + per-minute throttle
  keep usage at ~1–7 % of the soft limits; 429 maps to `RateLimited`; the
  pipeline already tolerates whole failed cycles (next tick retries).
- [Non-commercial license restriction] → njord is a private Home Assistant
  bridge; documented in CLAUDE.md so a future commercial use triggers the
  paid-tier discussion.
- [Open-Meteo renames or retires model ids] → ids live in config, not code;
  the affected pair degrades to `ModelUnavailable` per cycle without killing
  the service; the cycle summary log makes it visible.
- [Coarser models are interpolated to hourly by Open-Meteo] → acceptable: the
  consensus consumes per-model values as-is; spread diagnostics may look
  slightly smoother at far horizons. Noted, not mitigated.
- [Naive trust in response column order] → parse by JSON property name, never
  by array position across variables; unit assertion (D2) catches unit drift.

## Migration Plan

No deployment exists; this is a pre-production swap. One PR, conventional
commits, no data migration. Config files change shape
(plan → budget-only, new model ids) — `appsettings.json` is updated in the
same commit. CLAUDE.md's Kachelmann facts section is replaced with the
verified Open-Meteo facts. Rollback = revert the commit.

## Open Questions

- None blocking. The default model list (D7) is a taste question the user can
  adjust in config at any time; horizons/quorum interplay lands in the
  consensus change.
