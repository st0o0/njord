## MODIFIED Requirements

### Requirement: ConsensusResult includes daily summaries aggregated from hourly consensus medians
`ConsensusResult` SHALL expose a `DailySummaries` property of type `IReadOnlyList<DailyConsensusSummary>`. Each entry represents one calendar day and is computed by grouping hourly consensus horizons into calendar days using the timezone carried on the `ModelForecast` data for that location. The grouping SHALL use floor-anchored times from `TimeAnchor.AtHorizon` (truncated to the start of the hour, not rounded up).

#### Scenario: Full day with 24 hourly medians
- **WHEN** hourly consensus has medians for temperature_2m at h0–h23 covering a single calendar day in timezone Europe/Zurich, with values [18, 19, 20, 22, 24, 26, 28, 30, 31, 32, 33, 33, 32, 31, 30, 28, 26, 24, 22, 20, 19, 18, 17, 16]
- **THEN** `DailySummaries[0].TemperatureMax` = 33 and `DailySummaries[0].TemperatureMin` = 16

#### Scenario: Partial day still uses all available hours
- **WHEN** the consensus was computed at 14:00 UTC and the current calendar day in the location's timezone has hourly medians from h0 (covering the morning hours) through h10 (covering up to midnight local)
- **THEN** `DailySummaries[0]` SHALL include all hours of the calendar day, including hours before the consensus computation time

#### Scenario: No forecasts available defaults to UTC
- **WHEN** no `ModelForecast` entries exist for the location in the current snapshot
- **THEN** calendar-day bucketing SHALL use UTC

#### Scenario: Day boundary aligns with local midnight
- **WHEN** the consensus is computed at 22:30 UTC (00:30 CEST) with floor-anchored h0 = 22:00 UTC (00:00 CEST)
- **THEN** h0 SHALL be grouped into the new calendar day (the day starting at 00:00 CEST), not the previous day
