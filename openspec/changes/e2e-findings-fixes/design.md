## Context

The 2026-10-21 end-to-end run found six user-visible defects (F1-F6). Each is small, but they touch several
libraries. The relevant code today:

- **Base fields (F1, F2).** `FeatureResolution.RequiredParams` ORs the `Requirements` of the enabled features into a
  `DomainRequirements`, wrapped as `ResolvedRequirements` in `DomainSetupContainer`. The clients build their
  `&hourly=` lists from it and `BudgetGuard` weighs the weather call from it. No feature requires `WeatherCode` or
  `Pm10`, so the weather entity and the PM10 sensor in ha-njord always see empty values.
- **Retry storm (F4).** `PollWorker.OnDomainFetchResult` persists a `DomainPolled` with a fixed delay per failure:
  60 s (`DefaultRetry`), 5 min for rate limit/budget, 5 s for pipeline unavailable. An inland location gets HTTP 400
  from the marine API every time, so Marine retries every 60 s forever (about 1,440 calls/day).
- **Seasonal (F6).** `SeasonalOutlook` projects `SeasonalWeatherInput(Seasonal, Weather)`. `FeatureEngine` dispatches
  a rule only for the domain that just arrived, and the projection needs both domains in the current
  `LocationForecast`, which in practice is never true for the Seasonal-triggered dispatch. Separately,
  `DomainDataUpdate` in `weather.proto` has no seasonal case, so seasonal domain data never reaches gRPC.
- **Logs (F5).** `PendingDomainRequest` keeps the unresolved `Location` (`Lucerne (0, 0)`) from the `FetchDomain`
  message; the resolved location is already in the record but replies and logs use the unresolved one.
- **Docs (F3).** `ecmwf_ifs025_ensemble_mean` returns no `visibility` and `uv_index`; Visibility and UV alerts never
  fire with the default model. This is a data fact, documented, not fixed.

## Goals / Non-Goals

**Goals:**

- Weather condition and PM10 are always populated when their domain is fetched.
- A domain that keeps failing costs at most one call per poll interval.
- Seasonal data reaches Home Assistant both as domain data and as the outlook enrichment.
- Logs show the real coordinates.

**Non-Goals:**

- Singleton proxy warnings (F7), commas in URLs (F8).
- A persistent "domain unavailable" state; the capped backoff bounds the cost already.
- Making proto fields optional with client-side fallbacks.
- Fetching visibility/UV from another model by default.

## Decisions

### Base parameters are added after resolution, not as feature requirements

`FeatureResolution.WithBaseParams(DomainRequirements)` adds `WeatherParams.WeatherCode` when any weather flag is set
and `AirQualityParams.Pm10` when any air-quality flag is set. `DomainSetupContainer` builds
`new ResolvedRequirements(FeatureResolution.WithBaseParams(FeatureResolution.RequiredParams(rules)))`. Clients and
`BudgetGuard` consume `ResolvedRequirements` and pick up the fields automatically; `FeatureEngine` keeps gating on each
rule's own `Requirements`, so a missing `weather_code` never skips a rule.

- *Alternative:* a pseudo-feature (`Weather:Entity`) that requires them. Rejected: it would show up as a toggle,
  in `features.json` and in the active enrichments, and could be switched off.
- *Alternative:* add the flags as `Optional*` requirements to existing features. Rejected: arbitrary which feature
  owns them, and they disappear when that feature is off.

### Backoff is a pure function of the consecutive-failure count

`DomainPollState` gains `ConsecutiveFailures` (int, default 0). On a failed outcome the count goes up by one and the
delay is `min(60 s * 2^(n-1), pollInterval)`; rate limit and budget take `max(that, 5 min)`; pipeline unavailable
keeps 5 s and does not change the count; a `Changed` or `Unchanged` outcome resets it to 0. The delay computation
lives in a static `FailureBackoff` helper in `Njord.Polling` so it is tested without actors.

`DomainPolled` gains a trailing defaulted member `int ConsecutiveFailures = 0`; `Apply` copies it into the domain
state, so recovery reproduces the count and old journal entries recover as 0. `PersistedDomainState` (snapshot)
gains the same defaulted member.

The poll interval reaches `PollWorker` through a new constructor parameter `IOptions<PollingOptions>` (resolved by
`resolver.Props<PollWorker>(entityId)` like the other dependencies); tests pass `Options.Create(new PollingOptions())`.

- *Alternative:* cap at a fixed 60 min. Rejected: the cap should follow the operator's poll interval.
- *Alternative:* derive the count from the journal on recovery without a new field. Rejected: snapshots delete the
  journal, so the count would be lost.
- *Alternative:* stop polling the domain after N failures. Rejected: transient outages would need a restart.

### SeasonalOutlook takes seasonal data alone

`SeasonalWeatherInput` is replaced by `SeasonalInput(DomainForecast<SeasonalPoint> Seasonal)` in `FeatureInputs.cs`
(no other user). The outlook only uses the seasonal anomalies and means; the weather forecast was never read in
`Evaluate`.

### Seasonal domain data on gRPC

`weather.proto` adds:

```proto
message SeasonalSample {
  google.protobuf.Timestamp valid_at = 1;
  optional double temperature_anomaly = 2;
  optional double precipitation_anomaly = 3;
  optional double precipitation_mean = 4;
}
message SeasonalData { repeated SeasonalSample points = 1; }
// DomainDataUpdate.data oneof
SeasonalData seasonal = 16;
```

`DomainDataProtoMappingExtensions.ToDomainDataUpdate` maps `DomainForecast<SeasonalPoint>`; `GrpcConsumerState` keeps
the latest seasonal forecast per location like the other domains, so `GetDomainData(DOMAIN_SEASONAL)` and the filtered
stream both deliver it. The wire change is additive (new case, new messages).

- *Alternative:* reuse `SeasonalMonthEntry`. Rejected: that is the enrichment's derived shape (rounded anomaly, percent),
  domain data stays raw like the other domains.

### Gateway uses the resolved location everywhere

All replies (`FetchDomainCompleted/Failed/Rejected`) and log lines in `OpenMeteoGateway` use
`PendingDomainRequest.Resolved` (name and coordinates). PollWorker log lines keep using the location name only.

### Visibility / UV note

`docs/data/features.json` descriptions of `Weather:Visibility` and `Weather:Uv` and the weather models docs page note
that the default ensemble model delivers neither field; the alerts need a model that does.

## Risks / Trade-offs

- [`weather_code` crosses a multiple of 10 variables and adds a weather call per cycle] → `BudgetGuard` counts it
  because it reads the same `ResolvedRequirements`; `docs/.vitepress/theme/builder/types.ts` adds the base field to its variable count so the
  Config Builder matches.
- [A domain that recovers after hours of failures waits up to one poll interval for its first retry] → acceptable;
  `TriggerPoll` still polls immediately and a success resets the count.
- [Persisted record shape changes] → trailing defaulted members keep old journals readable; 0.x allows it anyway.
- [ha-njord must regenerate protos to see seasonal data] → additive change, old clients ignore case 16; the
  ha-njord counterpart ships in the same release.

## Migration Plan

No operator action. Existing journals recover with a failure count of 0. ha-njord picks up the new proto in its
counterpart change.

## Open Questions

None.
