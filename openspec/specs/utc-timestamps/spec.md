# utc-timestamps Specification

## Purpose

All Open-Meteo requests, domain timestamps and enrichment date comparisons use UTC exclusively; the domain model carries no timezone.
## Requirements
### Requirement: API requests use UTC timezone

The system SHALL send `timezone=UTC` in all Open-Meteo API requests. Combined with `timeformat=unixtime`, this ensures all returned timestamps represent midnight UTC boundaries.

#### Scenario: Daily timestamps align to midnight UTC

- **WHEN** the system requests a forecast with `timezone=UTC&timeformat=unixtime`
- **THEN** the first daily timestamp SHALL be midnight UTC of the current day (e.g. `2026-07-30T00:00:00Z`)

#### Scenario: Hourly timestamps align to midnight UTC

- **WHEN** the system requests a forecast with `timezone=UTC&timeformat=unixtime`
- **THEN** the first hourly timestamp SHALL be `00:00 UTC` of the current day

### Requirement: No timezone parsing from API response

The system SHALL NOT parse the `timezone` field from the Open-Meteo API response into a `TimeZoneInfo` object. The field MAY still be present in the response DTO but SHALL be ignored.

#### Scenario: Unrecognized timezone does not cause failure

- **WHEN** the API response contains any value in the `timezone` field (including unrecognized strings)
- **THEN** the system SHALL NOT return a failure outcome based on that field

### Requirement: No timezone in the Forecast record
The `Forecast` record (src/Njord.Weather/Forecast.cs) SHALL NOT carry a `TimeZoneInfo` property. All timestamp fields (`ForecastPoint.ValidAt`, `DailyForecastPoint.Date`, `CycleId.Timestamp`) SHALL represent UTC values exclusively. Forecasts are not persisted, so no reconstruction path needs a time zone.

#### Scenario: Forecast construction without timezone
- **WHEN** a `Forecast` is constructed from an API response
- **THEN** its constructor takes exactly five parameters — `WeatherModel`, `string` (location), `CycleId`, `ForecastSeries`, `DailyForecastSeries` — and no `TimeZoneInfo`

