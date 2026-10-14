# pipeline-commands Specification

## Purpose

Fetch command protocol between the `PollScheduler` and the `OpenMeteoGateway`: the gateway assigns each `FetchForecast` its API weight and a `CycleId` before it enters the fetch stream.

## Requirements
### Requirement: The gateway assigns each fetch an API weight and a CycleId
`OpenMeteoGateway` SHALL compute one integer API weight at construction as `ceil(hourlyVariableCount / 10) x ceil(forecastDays / 14)` from the resolved parameter set and `NjordOptions.ForecastDays`, and SHALL attach it to every internal `FetchTarget`. Each `FetchTarget` SHALL also carry a `CycleId` stamped when the gateway receives the `FetchForecast`. `PollScheduler` SHALL send only the location and model (`FetchForecast(LocationId, string ModelId)`); there is no `WeightedTarget` and no `RefreshLocation` command.

#### Scenario: Default configuration yields weight 1
- **WHEN** the configuration has 9 hourly variables and 4 forecast days
- **THEN** each target's weight is `ceil(9/10) x ceil(4/14)` = 1

#### Scenario: Extended configuration yields higher weight
- **WHEN** the configuration has 15 hourly variables and 4 forecast days
- **THEN** each target's weight is `ceil(15/10) x ceil(4/14)` = 2

#### Scenario: Extended forecast days increase weight
- **WHEN** the configuration has 9 hourly variables and 16 forecast days
- **THEN** each target's weight is `ceil(9/10) x ceil(16/14)` = 2

#### Scenario: CycleId reflects the time of the request
- **WHEN** a scheduled poll for (lucerne, icon_d2) reaches the gateway at 12:00:00 UTC
- **THEN** the `FetchTarget` carries a `CycleId` with timestamp 12:00:00 UTC

