## Why

Njord answers "what is the weather over the next days". Seasonal outlook adds the long-range piece: "next month is expected 2°C warmer and 15% drier than normal." Open-Meteo provides a free Seasonal Forecast API (7-month outlook, 51 ensemble members, 6-hourly) that no other HA integration exposes.

The Climate Change API and everything built on it (`IClimateBaselineStore`, `ClimateBaselineService`, `MonthlyBaseline`, `ClimateAnomalyHandler`, `ClimateResponseMappings`) was removed in the forecast restructuring. Seasonal outlook therefore ships its own, self-contained baseline mechanism and does not depend on any shared climate component.

## What Changes

- New `ISeasonalClient` + `SeasonalClient` in `src/Njord.OpenMeteo/` - thin typed client for `seasonal-api.open-meteo.com/v1/seasonal`, built with `OpenMeteoRequestBuilder`. Like the other clients it receives its variables from outside; it owns no variable list.
- New `SeasonalResponse` / `SeasonalSixHourlyData` response models registered in `OpenMeteoJsonContext`.
- New `SeasonalOutlookService` (`BackgroundService`) in `src/Njord.OpenMeteo/` - fetches once per day per location, aggregates 6-hourly data to monthly values, derives monthly anomalies against its own baseline (see Baseline), and writes the result to `ISeasonalOutlookStore`. It is standalone: not an actor, not part of the PollWorker/Gateway path.
- New `ISeasonalOutlookStore` + in-memory `SeasonalOutlookStore` in `src/Njord.Core/Domain/` (thread-safe, keyed by location, overwritten per fetch).
- New `SeasonalOutlookHandler` (`IDomainHandler`) in `src/Njord.Weather/Lifestyle/`: `ConfigKey = "SeasonalOutlook"`, `Requirements = new(Weather: WeatherParam.Temperature)` so it piggybacks on weather updates and adds no extra fetched variables beyond what other handlers need. It reads the store and emits `PipelineEvent.SeasonalOutlookComputed`.
- New `PipelineEvent.SeasonalOutlookComputed` with an `ImmutableArray<MonthlyOutlook>`, and the `MonthlyOutlook` record in `Njord.Messages` (Month "2026-11", TempAnomaly in °C, PrecipAnomaly in %, Confidence High/Medium/Low).
- `EnrichmentOptions.SeasonalOutlook` toggle (default false); mirrored in the Config Builder (`docs/.vitepress/theme/builder`, `docs/data/enrichment.json`) and `docker-compose.example.yml`.
- gRPC + MQTT wire support via `LifestyleMappings.ToLifestyleUpdate()` (new `LifestyleKind`, string map).
- ha-njord: seasonal outlook sensor entity.

## Baseline

The service must compare the seasonal forecast with a "normal". Decision order, verified in task 1.1 against the Open-Meteo seasonal docs before any code is written:

1. **Preferred:** the Seasonal API returns anomalies directly (anomaly variants of the temperature and precipitation variables relative to the model climatology). The service requests those and aggregates them; no baseline store, no second API.
2. **Fallback (only if the API lacks anomalies):** the service owns a private monthly-normal cache fetched from the Open-Meteo Archive API (`archive-api.open-meteo.com`, ERA5 daily data, 10-30 years, fetched once per location and refreshed rarely). This lives inside `SeasonalOutlookService` and is not a shared store. If chosen, the budget and design sections are updated in that task.

The budget below assumes option 1.

## Non-goals

- Sub-monthly resolution (weekly outlook) - ensemble spread at that granularity is too wide to be actionable.
- Extreme Forecast Index (EFI) interpretation.
- Seasonal data as a domain in the PollWorker or `DomainKind` - it updates daily, not hourly, and bypasses the Gateway.
- Resurrecting any Climate Anomaly component.

## Capabilities

### New Capabilities

- `seasonal-outlook`: ISeasonalClient, SeasonalOutlookService, ISeasonalOutlookStore, SeasonalOutlookHandler, monthly anomaly outlook.

### Modified Capabilities

- `enrichment-config`: `SeasonalOutlook` toggle added (21 toggles).

## Impact

- **Njord.OpenMeteo** (`src/Njord.OpenMeteo/`): client, response model, `SeasonalOutlookService`, `Mapping/SeasonalResponseMappings.cs`; registration in `Configuration/OpenMeteoSetupContainer.cs`.
- **Njord.Core** (`src/Njord.Core/`): `Domain/ISeasonalOutlookStore.cs`, `Configuration/EnrichmentOptions.cs`.
- **Njord.Weather** (`src/Njord.Weather/`): `Lifestyle/SeasonalOutlookHandler.cs`, registered in `ServiceCollectionExtensions.AddWeatherDomain`.
- **Njord.Messages** (`src/Njord.Messages/Pipeline/`): `SeasonalOutlookComputed`, `MonthlyOutlook`, `LifestyleMappings`.
- **Njord.Grpc / Njord.Mqtt**: only mapping spec coverage (existing lifestyle pipeline).
- **API budget**: +1 call/day/location (seasonal API, weight 1), about 30 calls/month per location, against 300k/month and 10k/day; negligible next to the poll cycle. It is not a polled domain, so `BudgetCalculator` does not count it; it is documented as out-of-band and covered by the 80 % startup margin (task 2.4 checks whether an existing budget hook should record it). Politeness: one request per location per 24 h, retry after 15 min on failure, jittered first run so startup does not burst.
