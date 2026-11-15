# Data flow

This page explains how njord fetches weather data, feeds it through the analysis pipeline, and decides which feature rules to execute.

## Domains

njord fetches 6 independent data domains, each with its own API host and point type:

| Domain | API host | Point type | Typical cycle |
|--------|----------|------------|---------------|
| Forecast | api.open-meteo.com/v1/forecast | ForecastPoint | ~1 h |
| Ensemble | ensemble-api.open-meteo.com/v1/ensemble | EnsemblePoint | ~6 h |
| AirQuality | air-quality-api.open-meteo.com | AirQualityPoint | ~1 h |
| Flood | flood-api.open-meteo.com | FloodPoint | ~24 h |
| Marine | marine-api.open-meteo.com | MarinePoint | ~1-3 h |
| Seasonal | seasonal-api.open-meteo.com | SeasonalPoint | ~24 h |

Each domain is polled independently per location. There are no "rounds" where all domains are fetched together.

## Poll cycle

<likec4-view view-id="poll-flow"></likec4-view>

### PollWorker: per-location, per-domain timers

Each location gets a PollWorker (persistent sharded entity). Each domain has independent state:

- **Discovery** (default 20 min interval): polls until two consecutive hash changes have intervals within +/-20% of each other
- **Steady**: polls at the learned cycle minus 30 s buffer
- **Miss tolerance**: in Steady, unchanged hashes cause exponential backoff (2^(n-1) min, max 15 min). After 5 consecutive misses, falls back to Discovery
- **Failure backoff**: 60 s * 2^(n-1), capped at the poll interval. Rate-limited and budget rejections wait at least 5 min

The worker sends `FetchDomain` as Tell to the gateway and starts a 90 s reply timer.

### Change detection

The gateway computes a value-based hash on the forecast data (`DomainForecast.GetHashCode()`). The worker compares it with `LastHash`. Changed hashes trigger a new ring entry in the pipeline; unchanged hashes are a no-op.

## OpenMeteoGateway: stream-based dispatch

The gateway is a singleton with an internal Akka.Streams pipeline:

1. **Queue** (capacity 64, DropNew): incoming `FetchDomain` messages
2. **BudgetGateStage**: token-bucket rate limiter (80% of `RequestsPerMinute`)
3. **SelectAsyncUnordered(3)**: concurrent HTTP fetches via the domain client
4. **WireTap**: maps result to a reply for the PollWorker shard region
5. **Where(success)**: only successes continue to the pipeline
6. **SelectMany**: per-model entries first (ModelId set), then the primary (ModelId null)
7. **Buffer(16, DropHead)**: backpressure buffer
8. **To(SinkRef)**: the pipeline's MergeHub input

<likec4-view view-id="ingest"></likec4-view>

## Feature pipeline

<likec4-view view-id="pipeline-flow"></likec4-view>

The `ForecastPipeline` singleton owns both hubs:

- **MergeHub** (per-producer buffer 16): receives `DomainDataReceived`
- **BroadcastHub** (buffer 2): emits `PipelineEvent[]` to consumers

Between them sits `StatefulSelectMany` with a `FeatureEngine` closure.

### FeatureEngine: the rule dispatch core

The engine maintains a `Dictionary<locationName, RingBuffer<LocationForecast>>` (newest first). Each `LocationForecast` accumulates the latest data from all domains.

When a `DomainDataReceived` arrives:

1. Build `current` from the newest ring entry, replacing only the arrived domain's data
2. `ring.Push(current)` - a new slot
3. For each enabled rule:
   - Skip if `!descriptor.Requirements.Requires(arrived domain)` - the rule does not read this domain
   - Skip if `!current.HasFields(requirements)` - required data is missing
   - Call `rule.Project(current)` then `rule.Evaluate(context)`
4. Collect all non-null results as `PipelineEvent`s

### When does a rule run?

A rule runs when ALL of these are true:

1. **Enabled**: the feature's config has `Enabled = true`
2. **Domain match**: `Requires(arrived domain)` is true (checks both required and optional domains)
3. **Data present**: `HasFields(requirements)` checks only REQUIRED params; optional params never gate execution
4. **Projection succeeds**: `Project(ring[0])` returns non-null (multi-domain rules check all inputs here)

### Multi-domain accumulation

`LocationForecast` carries forward data from other domains. When Forecast data arrives, `current.AirQuality` carries the last-known AQ data. Rules requiring both domains start running once both have arrived at least once. There is no staleness check on accumulated data.

## Feature requirements and API variables

Each rule declares `DomainRequirements` in its `FeatureDescriptor`:

- **Required params**: the API must deliver these fields; `HasFields` gates execution
- **Optional params**: requested from the API but never gate execution; the rule tolerates nulls

At startup, `IOptions<DomainRequirements>` OR-combines all enabled rules' requirements and adds base params (`WeatherCode` for Forecast). The API clients request exactly those variables.
