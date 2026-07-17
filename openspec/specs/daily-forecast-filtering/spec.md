# daily-forecast-filtering Specification

## Purpose

Filtering of daily forecast entries at ingest to exclude days with no usable data, and computation of effective forecast days based on model horizon coverage.

## Requirements

### Requirement: Daily forecast entries with all-null values are filtered at ingest
When mapping Open-Meteo API daily data to `DailyForecastPoint` entries, the ingest layer SHALL exclude entries where all numeric and meta values are null. Days where at least one parameter has a non-null value SHALL be included.

#### Scenario: Fully covered day is included
- **WHEN** a daily entry has `temperature_2m_max = 25.7` and `precipitation_sum = 3.2`
- **THEN** the entry SHALL be included in the `DailyForecastSeries`

#### Scenario: Partially covered day is included
- **WHEN** a daily entry has `temperature_2m_max = 25.7` but `weather_code = null`
- **THEN** the entry SHALL be included (at least one value is non-null)

#### Scenario: All-null day is excluded
- **WHEN** a model's horizon only partially covers the last calendar day and all daily aggregate values are null
- **THEN** the entry SHALL NOT be included in the `DailyForecastSeries`

### Requirement: EffectiveForecastDays uses ceiling to preserve hourly coverage
`ModelCoverageRegistry.EffectiveForecastDays` SHALL compute the maximum number of days as `ceil(MaxForecastHours / 24)` to ensure all hourly data within the model's horizon is requested. The resulting last-day daily entry may have null values if the model does not fully cover that day — these are handled by the all-null filter above.

#### Scenario: 60-hour model requests 3 days
- **WHEN** a model has `MaxForecastHours = 60` and the configured `ForecastDays = 4`
- **THEN** `EffectiveForecastDays` SHALL return 3 (`ceil(60/24) = 3`, `min(4, 3) = 3`)
- **AND** hourly data for hours 0-60 SHALL be available
- **AND** the third daily entry MAY be filtered if all its values are null
