# forecast-api-options Specification

## Purpose
Independent configuration of the Forecast API (`Njord:Forecast`): forecast days, past hours, optional multi-model selection and budget weight.

## Requirements

### Requirement: Independent Forecast API configuration section
The system SHALL read Forecast API settings from the `Njord:Forecast` configuration section. The section SHALL contain `ForecastDays` (integer, default 4, range 1..16), `PastHours` (integer, default 0, range 0..96), and `Models` (string array, default empty).

#### Scenario: Defaults
- **WHEN** `Njord:Forecast` is not configured
- **THEN** the Forecast API client SHALL use ForecastDays=4, PastHours=0, and best_match model selection

#### Scenario: Custom values
- **WHEN** `Njord:Forecast:ForecastDays` is 7 and `Njord:Forecast:PastHours` is 24
- **THEN** the Forecast API request SHALL use `forecast_days=7` and `past_hours=24`

#### Scenario: ForecastDays above limit
- **WHEN** `Njord:Forecast:ForecastDays` is 17
- **THEN** startup validation SHALL fail naming the setting and its maximum of 16

#### Scenario: ForecastDays below minimum
- **WHEN** `Njord:Forecast:ForecastDays` is 0
- **THEN** startup validation SHALL fail

### Requirement: Forecast API multi-model selection
When `Njord:Forecast:Models` is populated, the Forecast API client SHALL send a `models` parameter with the configured model IDs. When it is empty, the client SHALL use best_match (no `models` parameter).

#### Scenario: Empty models (default)
- **WHEN** `Njord:Forecast:Models` is empty or not configured
- **THEN** the Forecast API request SHALL omit the `models` parameter and use best_match

#### Scenario: Explicit models
- **WHEN** `Njord:Forecast:Models` is `[icon_seamless, gfs_seamless]`
- **THEN** the Forecast API request SHALL include `models=icon_seamless,gfs_seamless` and the response SHALL be split into per-model forecasts

#### Scenario: Duplicate models rejected
- **WHEN** `Njord:Forecast:Models` contains a duplicate entry
- **THEN** startup validation SHALL fail naming the duplicate

### Requirement: Forecast API budget accounting
The Forecast API call weight SHALL be `ceil(variables / 10) * ceil(ForecastDays / 14)` with no model multiplier when using best_match, or multiplied by the model count when explicit models are configured.

#### Scenario: Default best_match weight
- **WHEN** 11 forecast variables are required and ForecastDays is 4
- **THEN** the Forecast API call weight SHALL be 2

#### Scenario: Multi-model weight
- **WHEN** 11 forecast variables, ForecastDays is 4, and 2 explicit models are configured
- **THEN** the Forecast API call weight SHALL be 4
