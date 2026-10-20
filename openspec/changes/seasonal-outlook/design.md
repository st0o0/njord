## Context

The earlier "slow domain" pattern (climate baseline service + store + Climate Anomaly handler) no longer exists; the Climate Change API, `IClimateBaselineStore`, `ClimateBaselineService`, `MonthlyBaseline`, `ClimateAnomalyHandler` and `ClimateResponseMappings` were deleted. `Njord.OpenMeteo` currently has no hosted service; its gateway path (`OpenMeteoGateway` -> thin typed clients) fetches only `DomainKind` domains (Weather, AirQuality, Flood, Marine) with variables and forecast days supplied from outside.

Current handler model: `IDomainHandler` has `ConfigKey`, `DomainRequirements Requirements` (record struct of `[Flags]` params per domain, combined with `|`) and `Handle(current, previous, time)`. `EnrichmentResolution` aggregates the Requirements of enabled handlers (`ResolvedRequirements` is a DI singleton) and derives the fetched domains from non-default flags. Per-domain options exist (`WeatherOptions`, `AirQualityOptions`, `MarineOptions`, `FloodOptions`), registered in `OpenMeteoSetupContainer`.

Seasonal outlook is a new standalone feature: a daily background fetch, an in-memory store, and a handler that surfaces the stored result on weather updates.

## Goals / Non-Goals

**Goals:** 3-month outlook of monthly temperature and precipitation anomalies with a confidence level. Daily fetch, enrichment-driven toggle, gRPC + MQTT wire.

**Non-Goals:** weekly resolution, EFI, seasonal as a `DomainKind`/PollWorker domain, any shared climate baseline store.

## Decisions

### D1: Standalone service, store, handler

`SeasonalOutlookService` (`BackgroundService` in `Njord.OpenMeteo`, daily fetch) writes to `ISeasonalOutlookStore` (interface and in-memory implementation in `Njord.Core/Domain`), and `SeasonalOutlookHandler` (`Njord.Weather/Lifestyle`) reads it. The service owns its loop (`TimeProvider`-driven timer, jittered first run, 24 h interval, 15 min retry, previous outlook kept on failure) and fetches only when `EnrichmentOptions.SeasonalOutlook` is enabled. It is not routed through the Gateway or PollWorker because Seasonal is not a `DomainKind`; this keeps the "one PollWorker per location, per-domain timers" guardrail intact. Feature libraries stay independent: the store interface lives in Core, so Weather and OpenMeteo meet only through it.

### D2: Baseline is self-contained

Preferred source: anomalies returned by the Seasonal API itself, so njord holds no baseline mechanism at all (task 1.1 confirms variable names and units). Fallback: a private monthly-normal cache inside `SeasonalOutlookService` fed by the Archive API; no shared store, no new public interface. This decision is the first gate of the task list.

### D3: Own 6-hourly to monthly aggregation

`SeasonalResponseMappings` (`src/Njord.OpenMeteo/Mapping/`, extension methods named `ToDomain`/`ToMessage`, stateless) groups the 6-hourly values by UTC year-month: mean for the temperature anomaly, relative percentage for the precipitation anomaly, and the standard deviation across the month for confidence. Months with fewer than half of the expected samples are dropped.

### D4: 3-month window

The handler emits the next 3 calendar months after `time.GetUtcNow()`. Spread widens beyond 3 months and seven months of "unknown" is worse than three actionable ones.

### D5: Confidence from spread

Per month, standard deviation of the temperature values: below 1.5°C High, below 3.0°C Medium, otherwise Low. Thresholds are constants pinned by specs; wire text comes from an explicit `ToWireName()` switch (no `ToString()`).

### D6: Handler wiring via Requirements

```csharp
public string ConfigKey => "SeasonalOutlook";
public DomainRequirements Requirements { get; } = new(Weather: WeatherParam.Temperature);
```

Registered with `services.AddSingleton<IDomainHandler, SeasonalOutlookHandler>()` in `AddWeatherDomain` (`src/Njord.Weather/ServiceCollectionExtensions.cs`); takes `ISeasonalOutlookStore` by constructor. `ConfigKey` must match the new `EnrichmentOptions` toggle (`EnrichmentConfigKeySpec`). Requiring one already-common Weather param does not meaningfully change the fetched variable set. The hosted service is registered in `OpenMeteoSetupContainer` and gated by the toggle through `EnrichmentOptions`, not through `ResolvedRequirements` (which only describes polled domains).

### D7: Wire via LifestyleUpdate

The event flattens through `LifestyleMappings.ToLifestyleUpdate()` into a new `LifestyleKind` value with a string map (`month_1`, `month_1_temp_anomaly`, `month_1_precip_anomaly`, `month_1_confidence`, ...). gRPC (`type_name = "lifestyle"`) and MQTT (`{base}/{location}/lifestyle/{type}`) need no new proto message. Enum-to-wire conversions use explicit switches.

## Risks / Trade-offs

- **[API shape unverified]** Seasonal anomaly variables may not exist. Mitigated by the D2 gate in task 1.1 and the Archive fallback.
- **[Seasonal skill]** Low past 3 months; mitigated by the 3-month window and confidence levels.
- **[Store is in memory]** After a restart the outlook is empty until the first fetch; the service fetches shortly after startup (jittered) and the handler emits nothing meanwhile.
- **[Budget]** +1 call/day/location, outside `BudgetCalculator`; documented in the proposal.
- **[Location coordinates]** The service needs coordinates per location. `LocationOptions.Latitude/Longitude` are optional and no resolver service remains in the tree; task 2.4 confirms where resolved coordinates come from and skips locations without them with a warning.
