## Context

Current model types (LocationId, CycleId, Forecast, ForecastSeries) are weather-only. The multi-domain architecture needs a generalized data model. All new types are additive — they coexist with old types until `cleanup-legacy`.

## Goals / Non-Goals

**Goals:** Define all new core types with full test coverage, usable by Phase 3 changes.
**Non-Goals:** Migrating existing code to use new types (that's Phase 3).

## Decisions

### D1: Location replaces LocationId — closed record struct with Lat/Lon

`readonly record struct Location(string Name, double Latitude, double Longitude)` with equality on Name only (case-insensitive). Coordinates are payload, not identity. Resolves from config once, passed everywhere.

### D2: DomainForecast<T> is generic over point type

`record DomainForecast<T>(DomainKind Domain, DateTimeOffset ModelRun, TimeResolution Resolution, ImmutableArray<T> Points) : IDomainForecast where T : IHasValidTime`. No PollCycle/CycleId — ModelRun is data freshness.

### D3: LocationForecast is closed (not dictionary-based)

`record LocationForecast(Location Location, WeatherData? Weather, AirQualityData? AirQuality, FloodData? Flood, SolarData? Solar, MarineData? Marine)`. Closed for compile-time safety. A new domain means adding a nullable property — rare event, worth the safety.

### D4: ForecastQueries as static utility

Pure functions: `ValueAt<T>`, `Window<T>`, `Nearest<T>`, `Correlate<T1,T2>`. Enrichments use these to query domain forecasts at specific times or correlate across domains. No state, no allocation.

### D5: Enrichment framework in Njord.Core

`IEnrichment<TInput>` with `Evaluate(TInput input, TimeProvider time)`. `IProjector<TInput>` with `Project(LocationForecast current, LocationForecast? previous)`. `ProjectionGroup<TInput>` bundles one Projector with N Enrichments. `RequiredDomains` on Projector enables reactive evaluation.

### D6: New types live in Njord.Model (vocabulary) and Njord.Core (framework)

Location, DomainKind, TimeResolution, IHasValidTime, domain point types → Njord.Model. DomainForecast<T>, LocationForecast, ForecastQueries, enrichment interfaces → Njord.Core (alongside existing types).
