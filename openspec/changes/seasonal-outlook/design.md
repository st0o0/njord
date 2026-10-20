## Context

The Climate Change API and everything built on it (`IClimateBaselineStore`, `ClimateBaselineService`, `MonthlyBaseline`, `ClimateAnomalyHandler`) were deleted. The earlier "slow domain" workaround (a hosted service writing to a store) is not repeated: Seasonal becomes a proper domain that flows through the existing gateway, PollWorker and pipeline machinery.

## Goals / Non-Goals

**Goals:** outlook of monthly temperature and precipitation anomalies with a confidence level, delivered like every other domain (gateway fetch, per-domain cycle learning, budget accounting, health), feature-toggled.

**Non-Goals:** weekly resolution, EFI, any shared climate baseline store, gRPC/MQTT raw seasonal data (only the computed outlook is delivered).

## Decisions

### D1: Seasonal is a `DomainKind`

`DomainKind.Seasonal` (appended, so persisted ints stay stable), `TimeResolution.Monthly`, `SeasonalParams` (`TemperatureAnomaly`, `PrecipitationAnomaly`, `PrecipitationMean`) and `SeasonalPoint` in `Njord.Forecast/Seasonal`. `DomainRequirements` gains `Seasonal`/`OptionalSeasonal`, `LocationForecast` gains `Seasonal`, `PipelineEvent.SeasonalDataReceived` carries the forecast through the pipeline. The domain is required only when the `SeasonalOutlook` feature is enabled, so the PollWorker polls it only then.

### D2: Thin client in the gateway

`ISeasonalClient`/`SeasonalClient` (`seasonal-api.open-meteo.com/v1/seasonal`, `monthly=` variables supplied by the gateway from the resolved requirements) is dispatched by `OpenMeteoGateway` like the other clients, weight 1 per call, so budget tracking, health and the one-reply-per-request contract apply unchanged. `SeasonalResponseMappings.ToDomainForecast` keeps the current month plus `SeasonalOptions.ForecastMonths` months.

### D3: Anomalies come from the API (verified live)

Monthly variables: `temperature_2m_anomaly` (K), `precipitation_anomaly` (mm; there is no `precipitation_sum_anomaly`) and `precipitation_mean` (mm). The default response is the ensemble mean (51 members of ECMWF SEAS5); `forecast_months` is ignored and about six months are always returned, so the limit is applied client side. No baseline mechanism is needed.

### D4: `SeasonalOutlookHandler` is a normal `IDomainHandler`

It reads `current.Seasonal`, drops the current and past months, converts the precipitation anomaly to percent of the normal month (`mean - anomaly`) and emits `PipelineEvent.SeasonalOutlookComputed` with `MonthlyOutlook` entries. No store.

### D5: Confidence from lead time

The ensemble mean carries no member spread, so confidence follows the lead time in months: up to 1 High, 2 Medium, beyond Low (constants pinned by specs; wire text via an explicit `ToWireName()` switch).

### D6: Cycle learning and budget

The PollWorker's Discovery mechanism learns the update cycle of the domain. `BudgetCalculator` does not multiply Seasonal by the poll-interval cycles; it counts it as 30 calls per month and location. The Config Builder mirrors this (about 1 call/day/location).

### D7: Options

`SeasonalOptions` (`Njord:Seasonal:ForecastMonths`, default 3, 1..6) registered like the other per-domain options.

### D8: Wire via LifestyleUpdate

The outlook flattens through `LifestyleMappings.ToLifestyleUpdate()` into `LifestyleKind.SeasonalOutlook` with keys `month_<n>`, `month_<n>_temp_anomaly`, `month_<n>_precip_anomaly`, `month_<n>_confidence`. The proto `Domain` enum gains `DOMAIN_SEASONAL = 6` for poll-state reporting; no raw seasonal data update exists on the wire.

## Risks / Trade-offs

- **[Discovery cost]** Until two consistent update intervals are seen the domain polls every discovery interval (20 min); with a roughly daily cycle that is up to about 70 calls per day and location during discovery, once (the learned state is persisted).
- **[Seasonal skill]** Low past 3 months; mitigated by the 3-month default and confidence levels.
- **[Cycle length]** The API may change its data less often than daily; the learned cycle then simply becomes longer.
