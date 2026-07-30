## ADDED Requirements

### Requirement: API requests use UTC timezone

The system SHALL send `timezone=UTC` in all Open-Meteo API requests. Combined with `timeformat=unixtime`, this ensures all returned timestamps represent midnight UTC boundaries.

#### Scenario: Daily timestamps align to midnight UTC

- **WHEN** the system requests a forecast with `timezone=UTC&timeformat=unixtime`
- **THEN** the first daily timestamp SHALL be midnight UTC of the current day (e.g. `2026-07-30T00:00:00Z`)

#### Scenario: Hourly timestamps align to midnight UTC

- **WHEN** the system requests a forecast with `timezone=UTC&timeformat=unixtime`
- **THEN** the first hourly timestamp SHALL be `00:00 UTC` of the current day

### Requirement: No timezone in domain model

The `ModelForecast` record SHALL NOT carry a `TimeZoneInfo` property. All timestamp fields (`ForecastPoint.ValidAt`, `DailyForecastPoint.Date`, `CycleId.Timestamp`) SHALL represent UTC values exclusively.

#### Scenario: ModelForecast construction without timezone

- **WHEN** a `ModelForecast` is constructed from an API response
- **THEN** the constructor SHALL accept exactly six parameters: `WeatherModel`, `string` (location), `CycleId`, `ForecastSeries`, `DailyForecastSeries` — no `TimeZoneInfo`

#### Scenario: Persistence recovery without timezone

- **WHEN** a `ModelForecast` is reconstructed from a persisted snapshot
- **THEN** the reconstruction SHALL NOT require a timezone parameter or hardcode a fallback timezone

### Requirement: UTC-only enrichment comparisons

All enrichment features SHALL compute date references using UTC. The concept of "today" for daily forecast matching SHALL be `DateOnly.FromDateTime(now.UtcDateTime)` where `now` comes from `TimeProvider`.

#### Scenario: Daily consensus uses UTC dates

- **WHEN** `DailyConsensusSummary` groups hourly horizons into days
- **THEN** the grouping SHALL use `DateOnly.FromDateTime(utcTime.UtcDateTime)` without timezone conversion

#### Scenario: Alert evaluation uses UTC today

- **WHEN** `AlertEvaluator` determines "today" for daily alert thresholds
- **THEN** it SHALL use `DateOnly.FromDateTime(now.UtcDateTime)`

### Requirement: No timezone parsing from API response

The system SHALL NOT parse the `timezone` field from the Open-Meteo API response into a `TimeZoneInfo` object. The field MAY still be present in the response DTO but SHALL be ignored.

#### Scenario: Unrecognized timezone does not cause failure

- **WHEN** the API response contains any value in the `timezone` field (including unrecognized strings)
- **THEN** the system SHALL NOT return a failure outcome based on that field
