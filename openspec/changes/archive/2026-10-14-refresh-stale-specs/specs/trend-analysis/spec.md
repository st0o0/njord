## MODIFIED Requirements

### Requirement: Precipitation timing finds start and end of precipitation
`TrendFormulas.PrecipitationTiming` SHALL accept a `ForecastSeries`, a `ParameterDef` for precipitation, and a `DateTimeOffset` (now). It SHALL scan the next 24 hours for points where precipitation > 0. It SHALL return `(int? StartsInHours, int? EndsInHours)` -- the hours-from-now to the first and last non-zero precipitation point. If no precipitation is found, both SHALL be `null`.

#### Scenario: Rain starting in 3 hours ending in 8 hours
- **WHEN** the series has precipitation > 0 from T0+3h through T0+8h
- **THEN** StartsInHours is 3 and EndsInHours is 8

#### Scenario: No precipitation
- **WHEN** all precipitation values are 0 or null in the next 24h
- **THEN** both StartsInHours and EndsInHours are null

#### Scenario: Continuous precipitation from now
- **WHEN** precipitation is > 0 from T0+0h through T0+12h
- **THEN** StartsInHours is 0 and EndsInHours is 12

### Requirement: Extrema timing finds hour of max and min temperature
`TrendFormulas.ExtremaTiming` SHALL accept a `ForecastSeries`, a `ParameterDef` for temperature, and a `DateTimeOffset` (now). It SHALL scan the next 24 hours and return `(int? MaxInHours, int? MinInHours)` -- the hours-from-now to the maximum and minimum temperature. If fewer than 2 non-null temperature points exist, the result SHALL be `(null, null)`.

#### Scenario: Peak at midday, low at dawn
- **WHEN** the series has max temperature at T0+6h and min at T0+18h
- **THEN** MaxInHours is 6 and MinInHours is 18

#### Scenario: Insufficient data
- **WHEN** the series has fewer than 2 non-null temperature values in the next 24h
- **THEN** both are null
