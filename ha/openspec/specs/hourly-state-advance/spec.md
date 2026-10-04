# Capability: Hourly State Advance

## Purpose

Weather entities advance their current-state properties to match the current hour rather than always using the first hourly forecast entry. A periodic timer re-evaluates state at each hour boundary so the HA UI stays current without waiting for the next coordinator poll.

## Requirements

### Requirement: Weather entities select the current-hour forecast entry for state
Weather entities SHALL use the hourly forecast entry whose `valid_at` is closest to but not after `utcnow()` for all current-state properties (temperature, condition, humidity, pressure, wind speed, wind bearing, apparent temperature, cloud cover). If no such entry exists, properties SHALL return `None`.

#### Scenario: Forecast pushed at 14:00, current time is 16:30
- **WHEN** the forecast data contains hourly entries for 14:00, 15:00, 16:00, 17:00
- **AND** the current UTC time is 16:30
- **THEN** the entity's `native_temperature` returns the value from the 16:00 entry

#### Scenario: Forecast pushed at 14:00, current time is 14:10
- **WHEN** the forecast data contains hourly entries starting at 14:00
- **AND** the current UTC time is 14:10
- **THEN** the entity's `native_temperature` returns the value from the 14:00 entry

#### Scenario: All forecast entries are in the future
- **WHEN** the forecast data contains only hourly entries with `valid_at` after `utcnow()`
- **THEN** all current-state properties return `None`

#### Scenario: Forecast data is empty
- **WHEN** the forecast data has an empty hourly list
- **THEN** all current-state properties return `None`

### Requirement: Weather entities re-evaluate state every hour
Weather entities SHALL register a UTC time listener that fires at the top of each hour (minute=0, second=0). When fired, the entity SHALL trigger a HA state write to re-evaluate all properties with the updated current time.

#### Scenario: Hour boundary crosses
- **WHEN** the clock advances from 15:59 to 16:00
- **THEN** the entity triggers `async_write_ha_state()`
- **AND** the current-state properties now return values from the 16:00 forecast entry

#### Scenario: Entity removed
- **WHEN** a weather entity is removed from HA
- **THEN** the hourly time listener is cancelled and no further callbacks fire

### Requirement: Hourly forecast filters past entries
`_async_forecast_hourly()` SHALL exclude hourly entries whose `valid_at` is before `utcnow()`, returning only current and future entries.

#### Scenario: Mixed past and future entries
- **WHEN** the forecast has hourly entries for 14:00, 15:00, 16:00, 17:00, 18:00
- **AND** the current UTC time is 16:30
- **THEN** `_async_forecast_hourly()` returns entries for 17:00 and 18:00 only

#### Scenario: All entries in the past
- **WHEN** all hourly entries have `valid_at` before `utcnow()`
- **THEN** `_async_forecast_hourly()` returns `None`

### Requirement: Consensus entity refreshes state every hour
`NjordConsensusWeatherEntity` SHALL register the same hourly time listener to trigger `async_write_ha_state()` at each hour boundary. The horizon-based value selection (h0, h1, etc.) SHALL remain unchanged.

#### Scenario: Consensus entity refreshes at hour boundary
- **WHEN** the clock advances to a new hour
- **THEN** the consensus entity triggers `async_write_ha_state()`
- **AND** HA updates the "last updated" timestamp on the entity card
