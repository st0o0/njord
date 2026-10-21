## Why

The 2026-10-21 end-to-end run (njord + ha-njord + Home Assistant) passed every section but surfaced defects that
users see directly: the weather entity always shows clear sky because `weather_code` is never requested, PM10 is
always 0.0, an inland location burns about 1,440 marine calls a day on constant 60 s retries, the seasonal outlook
never reaches Home Assistant, and poll logs print `Lucerne (0, 0)`. These should be fixed before the 0.5.0 release.

## What Changes

- **Base fields for required domains.** The fetch request of every domain that is already required by an enabled
  feature also carries that domain's base fields: `weather_code` for Weather, `pm10` for AirQuality. Base fields
  never add a domain and never gate a rule; rule dispatch keeps using each feature's own requirements.
- **Exponential poll backoff on failures.** A domain that keeps failing (no usable data, transport error, HTTP error)
  is retried after 60 s, then 2, 4, 8 ... minutes, capped at the poll interval (60 min). A success resets the
  backoff. Rate-limit/budget rejections keep their 5 min floor; pipeline-unavailable keeps its 5 s retry.
  The consecutive-failure count survives a restart (persisted with `DomainPolled`).
- **Seasonal over gRPC.** `DomainDataUpdate` gains a `SeasonalData seasonal = 16` case with monthly samples
  (time, temperature anomaly, precipitation anomaly, precipitation mean); `GetDomainData(DOMAIN_SEASONAL)` and
  `StreamDomainData` deliver it.
- **SeasonalOutlook emits.** The rule projects Seasonal data alone instead of Seasonal + Weather, so it fires when
  seasonal data arrives (it is dispatched only then and never had weather in the same input).
- **Coordinates in logs.** Gateway replies and logs carry the resolved location (name and coordinates).
- **Docs.** Document that the default ensemble model returns no `visibility` and `uv_index`, so the Visibility and
  UV alerts need another model to fire.
- ha-njord counterpart (separate change in `D:\GIT\ha-njord`): regenerate protos, consume the seasonal domain
  data, show the real weather condition and PM10.

## Non-goals

- ClusterSingletonProxy "singleton not found" warnings at startup.
- Percent-encoding list commas in `ClientRequest` (Open-Meteo accepts them).
- Making proto fields optional with client-side fallbacks instead of fetching base fields.
- A persistent "domain unavailable for this location" state; the capped backoff bounds the cost.
- Fetching visibility/UV from a second model for the default configuration.

## API budget impact

- **Base fields:** Weather weighs `ceil(variables / 10)` calls; one more variable raises the weight by one only
  when the count crosses a multiple of 10. Worst case +1 call per weather cycle, about +720 requests/month per
  location at a 60 min cycle (well inside 80 % of 300k for typical setups; `BudgetGuard` counts it, because it
  uses the same resolved requirements). PM10 adds no call (AirQuality weight stays 1).
- **Backoff:** an inland location with Marine enabled drops from about 1,440 calls/day (~43,000/month) to at most
  ~24 calls/day (~720/month) once the backoff reaches the 60 min cap, i.e. it never costs more than a healthy
  domain. Healthy domains are unaffected. Net effect is a large reduction, also against the 10k/day limit.
- Seasonal over gRPC changes no polling.

## Capabilities

### New Capabilities

- `poll-failure-backoff`: per-domain exponential retry delay after consecutive failed polls, capped at the poll
  interval, reset on success, persisted across restarts.

### Modified Capabilities

- `domain-requirements`: base fields are added to the fetch request of domains that are already required.
- `seasonal-outlook`: the outlook is computed from seasonal data alone.
- `grpc-multi-domain`: domain data updates include seasonal data.

## Impact

- `Njord.Core/Domain` (`FeatureResolution`, base params), `src/Njord/Configuration/DomainSetupContainer.cs`,
  `Njord.Polling` (`PollWorker`, `PollWorkerState`, `BudgetGuard` via resolved requirements),
  `Njord.Persistence` (`DomainPolled` gains a defaulted failure-count member),
  `Njord.OpenMeteo/OpenMeteoGateway.cs`, `Njord.Features/Forecasts/SeasonalOutlook`, `Njord.Core/Domain/FeatureInputs.cs`,
  `protos/njord/v2/weather.proto`, `Njord.Grpc/Mapping`, `Njord.Grpc` consumer state, docs (`docs/data/features.json`,
  feature pages), `docs/.vitepress/theme/builder/budget.ts` if the weather variable count changes.
- gRPC wire: additive (new oneof case 16, new message). ha-njord must regenerate its vendored protos.
- Tests: Core, Polling, Persistence, OpenMeteo, Features, Grpc, IntegrationTests.
