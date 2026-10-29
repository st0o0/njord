# Polling, Pipeline and Rule Execution

This document explains how njord fetches weather data from the Open-Meteo APIs, feeds it
through the analysis pipeline, and decides which feature rules to execute.

## Domains

njord fetches 7 independent data domains, each with its own API host and point type:

| Domain | API host | Point type | Typical cycle |
|--------|----------|------------|---------------|
| Forecast | api.open-meteo.com/v1/forecast | ForecastPoint | ~1 h |
| Ensemble | ensemble-api.open-meteo.com/v1/ensemble | EnsemblePoint | ~6 h |
| AirQuality | air-quality-api.open-meteo.com | AirQualityPoint | ~1 h |
| Flood | flood-api.open-meteo.com | FloodPoint | ~24 h |
| Marine | marine-api.open-meteo.com | MarinePoint | ~1-3 h |
| Seasonal | seasonal-api.open-meteo.com | SeasonalPoint | ~24 h |
| Satellite | satellite-api.open-meteo.com/v1/archive | SatelliteRadiationPoint | ~1 h |

Each domain is polled independently per location. There are no "rounds" where all domains
are fetched together. A domain learns its own update cycle through discovery.

## Data flow overview

```
Timer fires in PollWorker
       |
       v
  FetchDomain(location, domain)  --Tell-->  OpenMeteoGateway
       |                                         |
       |                                    BudgetGateStage (token bucket, delays)
       |                                         |
       |                                    IDomainClient.FetchAsync (HTTP GET)
       |                                         |
       |                                    WireTap: reply to PollWorker shard
       |                                         |
       v                                         v
  FetchDomainCompleted/Failed/Rejected      DomainDataReceived --> MergeHub
       |                                                              |
  DomainPolled (persisted)                                   FeatureEngine.Process
  Schedule next poll                                                  |
  Report to PollManager                                     Run matching rules
                                                                      |
                                                             PipelineEvent[]
                                                                      |
                                                              BroadcastHub
                                                                      |
                                                              GrpcConsumer
                                                                      |
                                                              gRPC to HA
```

## PollWorker: per-location, per-domain timers

Each configured location gets a PollWorker (persistent sharded entity). On startup it
receives its enabled domains from `FeatureRequirementsOptions.Domains` (the union of all
enabled features' requirements).

Each domain has independent state:

- **Phase**: Discovery (polls every `DiscoveryInterval`, default 20 min) or Steady
  (polls at the learned cycle minus 30 s buffer).
- **Cycle learning**: after two consecutive hash changes with intervals within +/-20% of
  each other, the domain transitions to Steady.
- **Miss tolerance**: in Steady, unchanged hashes cause exponential backoff (2^(n-1) min,
  max 15 min). After 5 consecutive misses, falls back to Discovery.
- **Failure backoff**: 60 s * 2^(n-1), capped at the poll interval. Rate-limited and
  budget rejections wait at least 5 min.

The worker sends `FetchDomain` as a Tell (fire-and-forget) to the gateway and starts a
90 s reply timer. The reply routes back through the shard region.

### Change detection

The gateway computes `DomainForecast.GetHashCode()` on the primary forecast. The worker
compares it with `LastHash`. If the hash changed, the outcome is `Changed`; otherwise
`Unchanged`. This is a value-based hash on the forecast data, not the HTTP response.

## OpenMeteoGateway: stream-based dispatch

The gateway is a singleton actor with an internal Akka.Streams pipeline:

1. **Queue** (capacity 64, DropNew): incoming FetchDomain messages
2. **BudgetGateStage**: token-bucket rate limiter (80% of `RequestsPerMinute`, burst
   `max(rate/30, 16)`). Delays, never rejects.
3. **SelectAsyncUnordered(3)**: concurrent HTTP fetches via the domain client
4. **WireTap**: maps result to a reply for the PollWorker
5. **Where(success)**: only successes continue to the pipeline
6. **SelectMany**: per-model entries first (ModelId set), then the primary (ModelId null)
7. **Buffer(16, DropHead)**: backpressure buffer
8. **To(SinkRef)**: the pipeline's MergeHub input

### Model data ordering

For Ensemble fetches with `Models` enabled, the gateway emits per-model
`DomainDataReceived` entries (with `ModelId` set) before the primary Ensemble entry.
This ensures models are merged into the ring before the primary triggers rule evaluation.

## ForecastPipeline: MergeHub to BroadcastHub

The pipeline singleton owns both hubs:

- **MergeHub** (per-producer buffer 16): receives `DomainDataReceived` from producers
- **BroadcastHub** (buffer 2): emits `PipelineEvent[]` to consumers

Between them sits `StatefulSelectMany` with a `FeatureEngine` closure.

If the analysis graph fails, it re-materializes with a fresh FeatureEngine (ring state
is lost). Hubs and consumers are unaffected.

## FeatureEngine: the rule dispatch core

The engine maintains a `Dictionary<locationName, RingBuffer<LocationForecast>>` (newest
first). Each `LocationForecast` is a composite of all domains' latest data for that
location.

### Processing a DomainDataReceived message

**Branch A: model data (ModelId != null)**

- Merge the model forecast into `ring[0].ModelForecast` via `WithModel`.
- No new ring entry is pushed (ReplaceNewest on existing).
- No rules run. Return only the raw data event.

**Branch B: regular domain data (ModelId == null)**

1. Build `current` from the newest ring entry (or empty), replacing only the arrived
   domain's data. Carry forward `ModelForecast`.
2. `ring.Push(current)` -- a new slot.
3. For each enabled rule:
   - Skip if `!descriptor.Requirements.Requires(received.Domain)` -- the rule doesn't
     care about this domain.
   - Skip if `!current.HasFields(requirements)` -- required data is missing.
   - Call `feature.Execute(ring, now)`.
4. Collect all non-null results as `PipelineEvent`s.

### When does a rule run?

A rule runs when ALL of these are true:

1. **Enabled**: `rule.Enabled` is true (from its config section).
2. **Domain match**: `Requires(arrived domain)` is true. This uses `AllX` (required |
   optional), so a rule re-evaluates when its optional data arrives too.
3. **Data present**: `HasFields(requirements)` checks only the REQUIRED params. Optional
   params never gate execution.
4. **Projection succeeds**: `Project(ring[0])` returns non-null. This is where
   multi-domain rules check that all their input domains have data.

### The ring and PollDepth

`FeatureRule.Execute` walks `ring[0..PollDepth]`, calling `Project` on each entry.
Non-null projections become the `History` list. `ctx.Current` is `History[0]` (the newest
projected entry).

**Important**: the ring is pushed on every domain arrival, not per poll cycle. A rule with
`PollDepth: 3` sees 3 recent domain arrivals, which may include arrivals from other
domains where the rule's own data didn't change. For single-domain rules (e.g.,
PollenTrend on AQ) this is fine because they only run on their own domain's arrival. For
multi-domain rules the history may contain duplicate data.

### Multi-domain accumulation

`LocationForecast` accumulates data across domains. When Forecast data arrives,
`current.AirQuality` carries the last-known AQ data. When AQ data arrives later,
`current.Forecast` carries the last-known Forecast data. Rules requiring both domains
will start running once both have arrived at least once.

There is no staleness check on accumulated data. If a domain stops updating (permanent
fetch failure), its last-known data stays in the ring indefinitely.

## Feature requirements and API variables

Each rule declares its `DomainRequirements` in its `Descriptor`:

- **Required params** (e.g., `Forecast: Temperature | Precipitation`): the API must
  deliver these fields, and `HasFields` gates execution on them.
- **Optional params** (e.g., `OptionalForecast: WindDirection`): requested from the API
  but never gate execution. The rule must tolerate nulls.

At startup, `FeatureResolution.Configure` OR-combines all enabled rules' requirements,
adds base params (`WeatherCode` for Forecast, `Pm10` for AirQuality), and configures the
API clients to request exactly those variables.

### Available ensemble spread variables

The Ensemble API provides per-hour spread (standard deviation across ensemble members):

| EnsembleParams | API variable | Currently used by |
|---------------|--------------|-------------------|
| TemperatureSpread | temperature_2m_spread | ConfidenceTrend, all ensemble alerts (confidence) |
| PrecipitationSpread | precipitation_spread | **Not used by any rule** |
| WindSpeedSpread | wind_speed_10m_spread | **Not used by any rule** |

All three are fully implemented (enum, point, API mapping). Rules currently derive
confidence from TemperatureSpread only, even for precipitation and wind alerts.

### The past_hours parameter

The `past_hours` HTTP parameter is supported (`ClientRequest.PastHours`) but only used by
the Satellite client. Rules like MigraineRisk and PressureDrop that need lookback data
beyond the current forecast's start (00:00 UTC today) cannot access it because the
Forecast client does not send `past_hours`.

### WeatherCode and freezing rain detection

`WeatherCode` is always requested as a base param. WMO codes 56/57 (freezing drizzle)
and 66/67 (freezing rain) are available in the forecast. The Ice alert currently does
not use `WeatherCode` and relies on temperature + precipitation instead.
