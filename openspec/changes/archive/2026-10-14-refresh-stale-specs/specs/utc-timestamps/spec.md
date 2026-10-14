## ADDED Requirements

### Requirement: No timezone in the Forecast record
The `Forecast` record (src/Njord.Weather/Forecast.cs) SHALL NOT carry a `TimeZoneInfo` property. All timestamp fields (`ForecastPoint.ValidAt`, `DailyForecastPoint.Date`, `CycleId.Timestamp`) SHALL represent UTC values exclusively. Forecasts are not persisted, so no reconstruction path needs a time zone.

#### Scenario: Forecast construction without timezone
- **WHEN** a `Forecast` is constructed from an API response
- **THEN** its constructor takes exactly five parameters — `WeatherModel`, `string` (location), `CycleId`, `ForecastSeries`, `DailyForecastSeries` — and no `TimeZoneInfo`

### Requirement: Analysis computes calendar days in UTC
All analysis SHALL compute date references in UTC from the injected `TimeProvider`. "Today" for daily forecast matching SHALL be `DateOnly.FromDateTime(now.UtcDateTime)`: `ConsensusBuilder` SHALL use it to align daily consensus, `HorizonProjection` to map day offsets to daily points, and `DaySlicer` SHALL split hours into day slices at UTC midnight.

#### Scenario: Daily consensus uses UTC today
- **WHEN** `ConsensusBuilder.Create` builds the daily consensus at 23:30 UTC
- **THEN** day offset 0 is the current UTC date, without time zone conversion

#### Scenario: Day slices split at UTC midnight
- **WHEN** `DaySlicer.Slice` runs at 22:00 UTC
- **THEN** the hours from 00:00 UTC onwards belong to day offset 1

## REMOVED Requirements

### Requirement: No timezone in domain model
**Reason**: The requirement named types, files or behavior that no longer exist; it is restated against the current code.
**Migration**: Superseded by "No timezone in the Forecast record" in this capability.

### Requirement: UTC-only enrichment comparisons
**Reason**: The requirement named types, files or behavior that no longer exist; it is restated against the current code.
**Migration**: Superseded by "Analysis computes calendar days in UTC" in this capability.
