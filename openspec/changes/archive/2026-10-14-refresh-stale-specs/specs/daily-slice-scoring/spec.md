## ADDED Requirements

### Requirement: DaySlicer splits consensus hours into calendar-day slices
`DaySlicer.Slice(ConsensusSnapshot consensus, DateTimeOffset now)` (src/Njord.Indices/DaySlicer.cs) SHALL return an `IReadOnlyList<DaySlice>` with up to 3 entries (d0, d1, d2), or an empty list when the consensus has no hourly cutoff. Each `DaySlice` SHALL expose its `DayOffset` (0, 1, or 2), `DayMeans` (parameter means from daylight hours), `NightMeans` (parameter means from nighttime hours), `FullDayMeans` (parameter means from all hours), `DaylightHoursCount`, and `NighttimeHoursCount`.

#### Scenario: Three full days available
- **WHEN** consensus has hourly data from h0 to h72 and `now` is `2026-08-04T06:00:00Z`
- **THEN** the result contains 3 `DaySlice` entries with `DayOffset` 0, 1, and 2

#### Scenario: Fewer than 3 days of data
- **WHEN** consensus `CutoffHour` is 36 and `now` is `2026-08-04T18:00:00Z`
- **THEN** the result contains 2 `DaySlice` entries (d0 and d1) because d2 has no consensus hours

#### Scenario: Empty day slice excluded
- **WHEN** a calendar day has zero consensus hours within the cutoff
- **THEN** that day does not appear in the result list

## MODIFIED Requirements

### Requirement: Hour-to-day mapping uses UTC midnight boundaries
Each consensus hour hN SHALL be mapped to an absolute timestamp `now + N hours`. The day offset SHALL be computed as `floor((absoluteTime - todayMidnightUtc).TotalDays)` where `todayMidnightUtc` is the UTC date of `now`. Hours mapping to day offsets > 2 SHALL be excluded.

#### Scenario: Hour at midnight boundary belongs to next day
- **WHEN** `now` is `2026-08-04T22:00:00Z` and hour h2 maps to `2026-08-05T00:00:00Z`
- **THEN** h2 SHALL be assigned to day offset 1 (tomorrow)

#### Scenario: Hour h0 always belongs to d0
- **WHEN** `now` is `2026-08-04T23:59:00Z`
- **THEN** h0 SHALL be assigned to day offset 0 (today)

#### Scenario: Late-day hours beyond d2 excluded
- **WHEN** `now` is `2026-08-04T06:00:00Z` and hour h70 maps to `2026-08-07T04:00:00Z`
- **THEN** h70 SHALL NOT be included in any day slice

### Requirement: DaySlice is a pure domain record
`DaySlice` SHALL be a sealed record in `Njord.Indices` containing `DayOffset` (int), `Means` (`PeriodMeans` with daylight, night and full-day dictionaries of `ParameterDef` to `double?`), `DaylightHoursCount` (int), `NighttimeHoursCount` (int), `SunshinePercent` (double?) and the optional confidence `Bounds` used for score envelopes. It SHALL expose `DayMeans`, `NightMeans` and `FullDayMeans` as views on `Means`, and `HoursIncluded` as the larger of the two hour counts.

#### Scenario: DaySlice record structure
- **WHEN** a `DaySlice` is created for d1 with 14 daylight hours and 10 nighttime hours
- **THEN** `DayOffset` is 1, `DaylightHoursCount` is 14, `NighttimeHoursCount` is 10 and `HoursIncluded` is 14

## REMOVED Requirements

### Requirement: TimeSliceAggregator splits consensus hours into calendar-day slices
**Reason**: The requirement named types, files or behavior that no longer exist; it is restated against the current code.
**Migration**: Superseded by "DaySlicer splits consensus hours into calendar-day slices" in this capability.
