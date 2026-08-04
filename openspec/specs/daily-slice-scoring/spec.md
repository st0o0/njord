# daily-slice-scoring Specification

## Purpose

Time-slice aggregation for index scoring. Splits consensus hourly data into calendar-day slices (d0/d1/d2) with day/night partitioning using the `is_day` consensus parameter. Provides `DaySlice` records with separated daylight, nighttime, and full-day parameter means for use by activity index scorers.

## Requirements

### Requirement: TimeSliceAggregator splits consensus hours into calendar-day slices
`TimeSliceAggregator.AggregateDaySlices` SHALL accept a `ConsensusSnapshot` and `TimeProvider`, and return an `IReadOnlyList<DaySlice>` with up to 3 entries (d0, d1, d2). Each `DaySlice` SHALL contain a `DayOffset` (0, 1, or 2), `DayMeans` (parameter means from daylight hours), `NightMeans` (parameter means from nighttime hours), `FullDayMeans` (parameter means from all hours), `DaylightHoursCount`, and `NighttimeHoursCount`.

#### Scenario: Three full days available
- **WHEN** consensus has hourly data from h0 to h72 and `timeProvider.GetUtcNow()` is `2026-08-04T06:00:00Z`
- **THEN** the result SHALL contain 3 `DaySlice` entries with `DayOffset` 0, 1, and 2

#### Scenario: Fewer than 3 days of data
- **WHEN** consensus `CutoffHour` is 36 and `timeProvider.GetUtcNow()` is `2026-08-04T18:00:00Z`
- **THEN** the result SHALL contain 2 `DaySlice` entries (d0 and d1) because d2 has no consensus hours

#### Scenario: Empty day slice excluded
- **WHEN** a calendar day has zero consensus hours within the cutoff
- **THEN** that day SHALL NOT appear in the result list

### Requirement: Hour-to-day mapping uses UTC midnight boundaries
Each consensus hour hN SHALL be mapped to an absolute timestamp `now + N hours`. The day offset SHALL be computed as `floor((absoluteTime - todayMidnightUtc).TotalDays)` where `todayMidnightUtc` is the UTC date of `timeProvider.GetUtcNow()`. Hours mapping to day offsets > 2 SHALL be excluded.

#### Scenario: Hour at midnight boundary belongs to next day
- **WHEN** `now` is `2026-08-04T22:00:00Z` and hour h2 maps to `2026-08-05T00:00:00Z`
- **THEN** h2 SHALL be assigned to day offset 1 (tomorrow)

#### Scenario: Hour h0 always belongs to d0
- **WHEN** `now` is `2026-08-04T23:59:00Z`
- **THEN** h0 SHALL be assigned to day offset 0 (today)

#### Scenario: Late-day hours beyond d2 excluded
- **WHEN** `now` is `2026-08-04T06:00:00Z` and hour h70 maps to `2026-08-07T04:00:00Z`
- **THEN** h70 SHALL NOT be included in any day slice

### Requirement: Day/night partitioning uses is_day consensus parameter
Within each calendar day, hours SHALL be classified as daylight (`is_day` median > 0.5) or nighttime (`is_day` median <= 0.5). `DayMeans` SHALL contain parameter means computed only from daylight hours. `NightMeans` SHALL contain parameter means computed only from nighttime hours. `FullDayMeans` SHALL contain parameter means from all hours regardless of `is_day`.

#### Scenario: Summer day with 16 daylight hours
- **WHEN** d1 has 24 consensus hours, 16 with `is_day` median > 0.5
- **THEN** `DaylightHoursCount` SHALL be 16 and `NighttimeHoursCount` SHALL be 8

#### Scenario: No daylight hours remaining today
- **WHEN** `now` is `2026-08-04T21:00:00Z` and all remaining d0 hours have `is_day` median <= 0.5
- **THEN** `DayMeans` SHALL have null values for all parameters and `DaylightHoursCount` SHALL be 0

#### Scenario: is_day parameter missing from consensus
- **WHEN** the consensus does not contain an `is_day` parameter
- **THEN** all hours SHALL be treated as daylight (fallback: `DayMeans` equals `FullDayMeans`)

### Requirement: DaySlice is a pure domain record
`DaySlice` SHALL be a sealed record in `Domain/Analysis/` containing: `DayOffset` (int), `DayMeans` (`IReadOnlyDictionary<ParameterDef, double?>`), `NightMeans` (`IReadOnlyDictionary<ParameterDef, double?>`), `FullDayMeans` (`IReadOnlyDictionary<ParameterDef, double?>`), `DaylightHoursCount` (int), `NighttimeHoursCount` (int).

#### Scenario: DaySlice record structure
- **WHEN** a `DaySlice` is created for d1 with 14 daylight hours and 10 nighttime hours
- **THEN** `DayOffset` SHALL be 1, `DaylightHoursCount` SHALL be 14, `NighttimeHoursCount` SHALL be 10

### Requirement: Parameter means use consensus medians
For each parameter in the consensus, the mean within a time window (day/night/full) SHALL be computed as the arithmetic mean of the `Median` values from `HorizonConsensus` entries at the qualifying hours. Hours with a null median for a parameter SHALL be excluded from that parameter's mean. If no qualifying hours have a non-null median, the parameter mean SHALL be null.

#### Scenario: Mean from 3 qualifying hours
- **WHEN** temperature medians at daylight hours h6, h7, h8 are 18.0, 19.0, 20.0
- **THEN** the `DayMeans` temperature SHALL be 19.0

#### Scenario: All nulls yield null mean
- **WHEN** all qualifying hours for a parameter have null medians
- **THEN** the parameter mean SHALL be null
