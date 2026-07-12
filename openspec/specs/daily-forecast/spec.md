# daily-forecast Specification

## Purpose

Daily aggregate forecast variables as HA sensors with day-offset horizons, fetched alongside hourly data from the Open-Meteo API and published via MQTT Discovery.

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

### Requirement: Daily sensors use day-offset horizons in HA
Daily parameters SHALL be exposed as HA sensors with day-offset horizons (`d0` = today, `d1` = tomorrow, etc.) derived from `forecast_days`. Their `unique_id` SHALL follow the pattern `njord_{location}_{model}_{param}_d{offset}`.

#### Scenario: Day-offset entity naming
- **WHEN** `forecast_days=4` and `temperature_2m_max` is active
- **THEN** discovery includes components with unique_ids ending in `_temperature_2m_max_d0`, `_temperature_2m_max_d1`, `_temperature_2m_max_d2`, `_temperature_2m_max_d3`

#### Scenario: Daily values appear in state JSON under day keys
- **WHEN** a cycle completes and a model delivered daily data
- **THEN** the state JSON contains keys `d0`, `d1`, etc. alongside the hourly `h3`, `h6`, etc.

### Requirement: Daily parameter count does not affect API call weight
Only hourly variable count determines the API call weight (`ceil(hourly_count / 10)`). Daily variables do not contribute to the weight calculation as per Open-Meteo documentation.

#### Scenario: Adding daily variables does not increase weight
- **WHEN** 15 hourly and 10 daily variables are active
- **THEN** the computed API call weight is `ceil(15/10) = 2`, not `ceil(25/10) = 3`
