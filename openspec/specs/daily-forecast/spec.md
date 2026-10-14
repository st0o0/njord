# daily-forecast Specification

## Purpose

Daily aggregate forecast variables fetched alongside the hourly data from the Open-Meteo API, identified by date, carried in the `Forecast` record without affecting the API call weight, and published as day-offset (`d{N}`) state topics.

## Requirements
### Requirement: Daily forecast values are fetched alongside hourly
The client SHALL request `daily` variables from the Open-Meteo API in addition to `hourly` variables when the active parameter set includes daily parameters. The `daily` query parameter SHALL list only the active daily variables.

#### Scenario: Request includes daily variables when configured
- **WHEN** the active parameter set includes daily parameters (e.g. Weather group with `temperature_2m_max`)
- **THEN** the HTTP request includes `&daily=temperature_2m_max,temperature_2m_min,...` alongside the hourly variables

#### Scenario: No daily request when no daily parameters active
- **WHEN** the active parameter set has been configured with only hourly parameters
- **THEN** the HTTP request does not include a `daily` query parameter

### Requirement: Daily forecast points use date-based identity
A daily forecast point SHALL be identified by `DateOnly` (the forecast date) rather than a `DateTimeOffset` hour. The daily series SHALL be ordered ascending by date.

#### Scenario: Daily series spans forecast_days
- **WHEN** `forecast_days=4` and the API returns daily data
- **THEN** the daily series contains points for today, tomorrow, +2d, and +3d

#### Scenario: Daily values include time-string parameters
- **WHEN** the API returns `sunrise` as `"2026-07-12T05:31"` for a date
- **THEN** the daily point for that date carries the sunrise value as a string

### Requirement: Daily parameter count does not affect API call weight
Only hourly variable count SHALL determine the API call weight (`ceil(hourly_count / 10)`). Daily variables do not contribute to the weight calculation as per Open-Meteo documentation.

#### Scenario: Adding daily variables does not increase weight
- **WHEN** 15 hourly and 10 daily variables are active
- **THEN** the computed API call weight is `ceil(15/10) = 2`, not `ceil(25/10) = 3`

### Requirement: Daily values are published on day-offset topics
Daily parameters SHALL be exposed with day-offset horizons (`d0` = today, `d1` = tomorrow, etc.) derived from the effective forecast days of the model. `MqttStatePublisher` SHALL publish one retained state message per day offset to `{baseTopic}/{location}/{model}/d{offset}`, next to the hourly `h{N}` topics. `TopicScheme.DailyUniqueId` SHALL name a daily value `njord_{location}_{model}_{param}_d{offset}` for consumers that build entity ids.

#### Scenario: Day-offset topics
- **WHEN** `forecast_days=4`, `temperature_2m_max` is active and the model delivers 4 days
- **THEN** state messages are published to `.../{model}/d0` through `.../{model}/d3`, each containing `temperature_2m_max`

#### Scenario: Day-offset unique id
- **WHEN** `TopicScheme.DailyUniqueId("lucerne", icon_d2, temperature_2m_max, 1)` is called
- **THEN** it returns `njord_lucerne_icon_d2_temperature_2m_max_d1`

