## Why

The current data model is weather-only: `Forecast`, `ForecastSeries`, `CycleId`, `LocationId` are all tied to a single Open-Meteo endpoint. The new multi-domain architecture (Weather, AirQuality, Flood, Solar, Marine) needs a unified model where each domain produces time-series data with its own resolution, and enrichments declare typed inputs projected from a `LocationForecast` read-model. This change lays the foundation for everything in Phase 3-4.

## What Changes

- New `Location` type replacing `LocationId` — carries Name + Lat/Lon, equality on Name (case-insensitive).
- New `DomainKind` enum (Weather, AirQuality, Flood, Solar, Marine).
- New `DomainForecast<T>` generic — time-series with ModelRun, Resolution, and `IHasValidTime` points.
- New `LocationForecast` read-model — Location + `ImmutableDictionary<DomainKind, IDomainForecast>`.
- New `ForecastQueries` utility — `ValueAt`, `Window`, `Nearest`, `Correlate` for temporal operations on domain forecasts.
- New `TimeResolution` type or enum (Hourly, ThreeHourly, SixHourly, QuarterHourly, Daily).
- New `IEnrichment<TInput>` interface + `IProjector<TInput>` + `ProjectionGroup` pattern.
- New `IEnrichmentInput` base interface with `Location` property.
- `CycleId` is removed — replaced by `ModelRun` on domain data (data freshness) and no poll-cycle tracking.
- `LocationId` is replaced by `Location` throughout (incremental migration).
- New domain point types: `WeatherPoint`, `AirQualityPoint`, `FloodPoint`, `SolarPoint`, `MarinePoint`.

## Non-goals

- Implementing actual domain fetchers or parsers — that's Phase 4.
- Changing the pipeline yet — that's `pipeline-projection-groups`.
- Removing old types (Forecast, ForecastSeries) — that's `cleanup-legacy`.
- API budget impact: zero (no polling changes).

## Capabilities

### New Capabilities

- `domain-forecast-model`: Core domain forecast types (Location, DomainKind, DomainForecast<T>, LocationForecast, TimeResolution, ForecastQueries).
- `enrichment-framework`: IEnrichment<TInput>, IProjector<TInput>, ProjectionGroup pattern for typed enrichment inputs.
- `domain-point-types`: Weather, AirQuality, Flood, Solar, Marine point record types with IHasValidTime.

### Modified Capabilities

_(none — all new types live alongside existing ones)_

## Impact

- **Njord.Model**: Extended with Location, DomainKind, TimeResolution.
- **New project or Njord.Core**: DomainForecast<T>, LocationForecast, ForecastQueries, enrichment interfaces.
- **Njord.Model.Tests**: New tests for all new types.
- All new code, no existing code changes. Old and new models coexist until `cleanup-legacy`.
