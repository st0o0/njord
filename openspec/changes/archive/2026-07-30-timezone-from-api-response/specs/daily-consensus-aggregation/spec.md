## MODIFIED Requirements

### Requirement: ConsensusResult includes daily summaries aggregated from hourly consensus medians
`ConsensusResult` SHALL expose a `DailySummaries` property of type `IReadOnlyList<DailyConsensusSummary>`. Each entry represents one calendar day and is computed by grouping hourly consensus horizons into calendar days using the timezone carried on the `ModelForecast` data for that location.

#### Scenario: Full day with 24 hourly medians
- **WHEN** hourly consensus has medians for temperature_2m at h0–h23 covering a single calendar day in timezone Europe/Zurich, with values [18, 19, 20, 22, 24, 26, 28, 30, 31, 32, 33, 33, 32, 31, 30, 28, 26, 24, 22, 20, 19, 18, 17, 16]
- **THEN** `DailySummaries[0].TemperatureMax` = 33 and `DailySummaries[0].TemperatureMin` = 16

#### Scenario: Partial day still uses all available hours
- **WHEN** the consensus was computed at 14:00 UTC and the current calendar day in the location's timezone has hourly medians from h0 (covering the morning hours) through h10 (covering up to midnight local)
- **THEN** `DailySummaries[0]` SHALL include all hours of the calendar day, including hours before the consensus computation time

#### Scenario: No forecasts available defaults to UTC
- **WHEN** no `ModelForecast` entries exist for the location in the current snapshot
- **THEN** calendar-day bucketing SHALL use UTC

## REMOVED Requirements

### Requirement: LocationOptions supports optional Timezone
**Reason:** Timezone is now derived from the Open-Meteo API response (`timezone=auto`), carried on `ModelForecast.TimeZone`, and extracted from snapshot data in `ConsensusEnrichment`. Manual configuration is no longer needed.
**Migration:** Remove any `Timezone` entries from location configuration. The timezone is automatically determined from coordinates via the API.
