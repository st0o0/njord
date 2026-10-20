## Context

`Njord.OpenMeteo` fetches only `DomainKind` domains through the Gateway; `Solar` exists in the model and event vocabulary only. `SolarYieldHandler` (`src/Njord.Weather/Lifestyle/`) currently sums the next 24 h of ensemble `DirectRadiation + DiffuseRadiation`, applies `SystemEfficiency` (0.8) and a heuristic `OrientationFactor` from `SolarYieldOptions` (`PeakKw`, `Azimuth` 180, `Tilt` 30), and emits `PipelineEvent.SolarYieldComputed`. `SolarPoint` has non-nullable doubles. The seasonal-outlook change introduces the standalone pattern (BackgroundService + Core store + handler); this change follows it.

## Goals / Non-Goals

**Goals:** measured radiation for recent hours, GTI for the configured panel orientation, a more accurate `SolarYield` for the current day, opt-in.

**Non-Goals:** historical analysis, replacing the ensemble forecast, sub-hourly updates, `Solar` as polled domain.

## Decisions

### D1: BackgroundService, store, handler (not a DomainKind)

`SatelliteRadiationService` (`BackgroundService` in `Njord.OpenMeteo`) writes to `ISatelliteRadiationStore` (interface and in-memory implementation in `Njord.Core/Domain`, thread-safe, keyed by location, overwritten per fetch); `SolarYieldHandler` reads it. This keeps the "one PollWorker per location, per-domain timers" guardrail intact and the feature libraries independent (they meet only in Core). The service runs only when `EnrichmentOptions.SatelliteRadiation` is enabled and skips locations without coordinates with a warning. All timing via `TimeProvider`.

### D2: Hourly fetch interval

Satellite data refreshes roughly every 15-60 minutes depending on the satellite, so an hourly fetch captures every useful update. The interval is a config value (default 60 min, validated, minimum 15 min for politeness) in a small options class registered with `AddNjordOptions<T>` (name decided in task 2.1). The first run is jittered; on failure retry after 15 min and keep the previous data.

### D3: Blend satellite (past/current) with ensemble (future)

For each hour of the 24 h window: hours at or before the latest satellite timestamp use satellite values, later hours use the ensemble forecast. The transition point is the last satellite timestamp that is not after `time.GetUtcNow()`. Satellite data older than a staleness bound (3 h, constant pinned by spec) is ignored and the handler falls back to pure ensemble, so a stalled service degrades to today's behavior. No smoothing at the seam in this change; it is a hard switch, a documented simplification.

### D4: GTI uses SolarYieldOptions.Tilt/Azimuth

The client receives `tilt` and `azimuth` from `SolarYieldOptions` (no new orientation config). Conversion of njord azimuth (180 = south) to the API convention is checked in task 1.1 and pinned by a client spec. When satellite GTI is available for an hour, the handler uses it directly and does not apply `OrientationFactor` to that hour (the orientation is already in the value); ensemble hours keep `OrientationFactor`. This avoids double counting.

### D5: Off by default

The toggle defaults to false: about 720 calls/month/location. `SolarYieldHandler.ConfigKey` stays `SolarYield`; `SatelliteRadiation` is a separate `EnrichmentOptions` toggle that only takes effect together with `SolarYield` (task 3.4 decides between validator warning and rejection). `EnrichmentConfigKeySpec` gets an allow-list entry if the toggle has no handler of its own.

### D6: Ensemble GTI - complement or replace

The Ensemble Mean API has Global Tilted Irradiance built in (tilt and azimuth request parameters). This changes the picture:

- **Future hours:** only the ensemble (or another forecast API) can supply them; satellite cannot. The ensemble stays authoritative for the forecast side, so satellite cannot fully replace it.
- **Recent hours:** satellite is measured, ensemble is modeled; satellite wins on accuracy, which is the purpose of this change. Here satellite complements the ensemble; it could replace the ensemble solar data for the observed window only.
- **Orientation:** ensemble GTI would make the heuristic `OrientationFactor` obsolete for forecast hours and give both sources the same physical basis (GTI), turning the blend into GTI-to-GTI. That requires adding GTI to the ensemble variable set (`WeatherVariables`, which changes the variable count and the `ceil(variables / 10)` budget weight, currently 4) and is therefore a separate, budget-relevant change.

Decision for this change: complement. Satellite GTI for observed hours, ensemble direct+diffuse with `OrientationFactor` for future hours. Task 1.1 verifies ensemble GTI availability and records the outcome; if confirmed, a follow-up change (`solar-yield-gti`) moves the forecast side to ensemble GTI and may make the satellite call's role purely "observed correction". The blend is written against a small per-hour "irradiance on panel" abstraction so that follow-up only swaps the forecast side.

### D7: Wire via existing Solar/Lifestyle path

`SolarYieldComputed` keeps its shape; it carries more accurate numbers when satellite data was used. Whether to expose the source mix is decided in task 5.1; if added, it is a new nullable/defaulted member flattened by `ToLifestyleUpdate()` (string map entry), with an explicit wire-name switch for any closed set. No new proto message.

### D8: SatelliteRadiationPoint

New positional record in `src/Njord.Forecast/Solar/` implementing `IHasValidTime`, all radiation members `double?` (the API returns nulls for missing slots): `GHI`, `DirectRadiation`, `DiffuseRadiation`, `DNI`, `GTI`. `SolarPoint` is not changed.

## Risks / Trade-offs

- **[API shape unverified]** Variable names, units, time resolution, regional satellite coverage and the azimuth convention are unconfirmed. Mitigation: task 1.1 gate; uncovered locations yield nulls and fall back to ensemble.
- **[Hard seam in the blend]** Possible step at the transition; accepted, measured data wins.
- **[Budget]** Opt-in, outside `BudgetCalculator`; documented in the proposal.
- **[In-memory store]** Empty after restart until the first fetch; the handler falls back to ensemble meanwhile.
- **[Double orientation correction]** Avoided by D4.
