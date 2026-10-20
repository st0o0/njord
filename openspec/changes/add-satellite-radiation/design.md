## Context

Seasonal was redesigned as a normal domain; satellite radiation follows the same path. `SolarYieldHandler` reads the ensemble `global_tilted_irradiance` (tilt/azimuth from `SolarYieldOptions` sent by `WeatherClient`) over the next 24 h and applies `SystemEfficiency`.

## Goals / Non-Goals

**Goals:** measured radiation for recent hours, GTI for the configured panel orientation, a more accurate `SolarYield` for the current day, opt-in.

**Non-Goals:** historical analysis, replacing the ensemble forecast, sub-hourly updates, a separate fetch loop.

## Decisions

### D1: API shape (verified against the live API)

The endpoint is `https://satellite-api.open-meteo.com/v1/archive` (not `satellite-radiation-api.../v1/radiation`, which does not resolve). Hourly variables `shortwave_radiation` (GHI), `direct_radiation`, `diffuse_radiation`, `direct_normal_irradiance`, `global_tilted_irradiance`, all W/m2, `null` where missing. `tilt` and `azimuth` use the same convention as the ensemble API (0 = south), so the compass azimuth of `SolarYieldOptions` (180 = south) is converted with `azimuth - 180`, exactly like `WeatherClient`. `past_hours` and `forecast_hours` select the window; the client sends `past_hours = SatelliteOptions.PastHours` and `forecast_hours = 1`. Times are requested with `timeformat=unixtime`, `timezone=UTC`. The ensemble API offers GTI too, which is what `SolarYield` already uses.

### D2: A normal DomainKind

`DomainKind.Satellite`, `SatelliteRadiationPoint` (hourly, `IHasValidTime`, `HasFields(SatelliteParams)`), `LocationForecast.Satellite`, `DomainRequirements.Satellite/OptionalSatellite`. The gateway dispatches `FetchDomain(Satellite)` to `ISatelliteRadiationClient`; the PollWorker learns the roughly hourly cycle via Discovery like any domain. No hosted service, store or special timers. The poll interval needs no hint: Discovery measures it.

### D3: Feature and cost

`FeatureKind.SatelliteRadiation` (config key `SatelliteRadiation`, Energy, off by default) requires `Satellite: GlobalTilted` and optionally `GlobalHorizontal`. With the default 60 min poll interval `BudgetCalculator` counts the domain once per poll cycle like Weather/AirQuality/Flood/Marine, which is about 720 calls/month/location; the Config Builder derives the same from `features.json`. `SolarYieldHandler` deliberately does NOT require the Satellite domain: enabling `SolarYield` alone must not add calls. It uses satellite data when the `SatelliteRadiation` feature delivers it.

### D4: Blend satellite (observed) with ensemble (forecast)

`SolarIrradianceBlend.Hours(forecast, now, 24)` walks the ensemble hours of the window. Hours at or before the newest satellite observation with a GTI use the satellite GTI, all other hours the ensemble GTI. The newest observation is the latest satellite point with a GTI whose `ValidFrom` is not after `now`; it is ignored when older than 3 h (`StaleAfter`), so a stalled domain degrades to ensemble-only. The seam is a hard switch, a documented simplification. The ensemble GTI already contains the panel orientation, so there is no separate orientation factor.

### D5: SatelliteRadiationHandler output

Triggered by Satellite arrivals (the engine only calls handlers that require the arrived domain). It emits `SatelliteRadiationComputed(Location, ObservedAt, GlobalTilted, GlobalHorizontal?, BlendedKwhPerM2?)`: the newest observed hour and, when ensemble GTI is known (SolarYield enabled), the blended 24 h irradiation in kWh/m2. It emits nothing without a usable observation. `SolarYieldHandler` itself is only re-evaluated on Weather arrivals, so its yield picks up fresh satellite data at the next weather cycle; the hourly view is the `SatelliteRadiationComputed` event.

### D6: Wire

gRPC gets `DOMAIN_SATELLITE = 7` and `SatelliteData`/`SatelliteSample` (optional doubles) in the domain data oneof. `SatelliteRadiationComputed` flattens through `ToLifestyleUpdate()` as `LifestyleKind.SatelliteRadiation` (`satellite_radiation`) with `observed_at`, `gti`, optional `ghi` and `blended_kwh_per_m2`. No new proto message for it.

## Risks / Trade-offs

- **[Hard seam in the blend]** Possible step at the transition; accepted, measured data wins.
- **[Coverage]** Regions outside the satellite footprint return nulls; the handler then emits nothing and SolarYield stays ensemble-only.
- **[SolarYield refresh]** SolarYield is recomputed per Weather arrival, not per Satellite arrival (D5).
