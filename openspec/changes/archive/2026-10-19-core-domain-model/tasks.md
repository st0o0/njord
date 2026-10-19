## 1. Location type

- [x] 1.1 Add `Location` readonly record struct to `src/Njord.Model/` (Name + Lat + Lon, case-insensitive Name equality)
- [x] 1.2 Add `LocationSpec` to `src/Njord.Model.Tests/` (equality, hash code, display)

## 2. Domain vocabulary types

- [x] 2.1 Add `DomainKind` enum to `src/Njord.Model/` (Weather, AirQuality, Flood, Solar, Marine)
- [x] 2.2 Add `TimeResolution` enum to `src/Njord.Model/` (QuarterHourly, Hourly, ThreeHourly, SixHourly, Daily)
- [x] 2.3 Add `IHasValidTime` interface to `src/Njord.Model/`
- [x] 2.4 Tests for DomainKind and TimeResolution

## 3. Domain point types

- [x] 3.1 Add `WeatherPoint` record to `src/Njord.Model/` implementing IHasValidTime
- [x] 3.2 Add `AirQualityPoint` record
- [x] 3.3 Add `FloodPoint` record
- [x] 3.4 Add `SolarPoint` record
- [x] 3.5 Add `MarinePoint` record
- [x] 3.6 Tests for all point types

## 4. DomainForecast<T> and LocationForecast

- [x] 4.1 Add `IDomainForecast` interface and `DomainForecast<T>` record to `src/Njord.Core/`
- [x] 4.2 Add `LocationForecast` record (closed, nullable domain properties)
- [x] 4.3 Add `LocationForecast.With(DomainKind, IDomainForecast)` update method
- [x] 4.4 Tests for DomainForecast and LocationForecast

## 5. ForecastQueries utility

- [x] 5.1 Add `ForecastQueries` static class to `src/Njord.Core/` with `ValueAt<T>`, `Window<T>`, `Nearest<T>`
- [x] 5.2 Add `Correlate<T1, T2>` for cross-domain temporal join
- [x] 5.3 Comprehensive tests for all query methods (hourly, 6-hourly, mixed resolution, edge cases)

## 6. Enrichment framework

- [x] 6.1 Add `IEnrichmentInput` interface (Location property)
- [x] 6.2 Add `IEnrichment<TInput>` interface (Evaluate method)
- [x] 6.3 Add `IProjector<TInput>` interface (Project method, RequiredDomains)
- [x] 6.4 Add `ProjectionGroup<TInput>` record
- [x] 6.5 Add `EnrichmentEvent` base type
- [x] 6.6 Tests for ProjectionGroup evaluation flow (mock enrichments, verify reactive routing)

## 7. Validation

- [x] 7.1 `dotnet build Njord.slnx` — all new types compile alongside existing
- [x] 7.2 `dotnet run --project Njord.Model.Tests/Njord.Model.Tests.csproj` — new model tests pass
- [x] 7.3 `dotnet run --project Njord.Core.Tests/Njord.Core.Tests.csproj` — new framework tests pass
- [x] 7.4 Full test suite — no existing tests broken
