# failure-routing Specification

## Purpose

Routing of failed fetches from the `OpenMeteoGateway` back to the `PollScheduler`: failures carry their location and model, and the scheduler retries every failure reason with transient backoff.

## Requirements
### Requirement: Fetch failures carry location and model context
Each `PollOutcome.Failure` returned by `IOpenMeteoClient.FetchAsync` SHALL carry the `Location` (string), `Model` (`WeatherModel`), `Reason` (`PollFailureReason`) and a `Detail` text of the failed fetch. The gateway SHALL forward it to the requester as `FetchForecastFailed(LocationId Location, string ModelId, PollFailureReason Reason, string? Detail)`, so the scheduler can identify which (location, model) pair failed.

#### Scenario: Transport failure carries target identity
- **WHEN** a fetch for (lucerne, icon_d2) returns HTTP 500
- **THEN** `PollScheduler` receives `FetchForecastFailed` with location `lucerne`, model id `icon_d2` and reason `Transport`

#### Scenario: Rate-limited failure carries target identity
- **WHEN** a fetch for (zurich, ecmwf_ifs025) returns HTTP 429
- **THEN** `PollScheduler` receives `FetchForecastFailed` with location `zurich`, model id `ecmwf_ifs025` and reason `RateLimited`

### Requirement: The scheduler retries failed fetches by reason
`PollScheduler` SHALL handle `FetchForecastFailed` by reason, without touching `MissCount`, `Phase` or the learned `Cycle`:

- **Transport**, **ModelUnavailable** and **MalformedPayload**: record a transient failure (`TransientFailureCount + 1`) and schedule a retry after the transient backoff (1 min, 2 min, 4 min, 8 min; from the fifth consecutive transient failure on, the discovery interval), logging a warning.
- **RateLimited**: record a transient failure and schedule the retry no earlier than 5 minutes from now, logging a warning.

#### Scenario: Transport failure triggers backoff retry
- **WHEN** a `Transport` failure arrives for (lucerne, icon_d2) with `TransientFailureCount = 0`
- **THEN** `TransientFailureCount` becomes 1 and the next retry is scheduled in 1 minute

#### Scenario: Second transport failure doubles backoff
- **WHEN** a `Transport` failure arrives for (lucerne, icon_d2) with `TransientFailureCount = 1`
- **THEN** `TransientFailureCount` becomes 2 and the next retry is scheduled in 2 minutes

#### Scenario: Rate-limited failure enforces minimum 5-minute delay
- **WHEN** a `RateLimited` failure arrives for (lucerne, icon_d2) with `TransientFailureCount = 0`
- **THEN** the next retry is scheduled in 5 minutes (not 1 minute)

#### Scenario: ModelUnavailable is retried with transient backoff
- **WHEN** a `ModelUnavailable` failure arrives for (lucerne, icon_d2)
- **THEN** a warning is logged and a retry is scheduled with the transient backoff

#### Scenario: Learned cycle survives failures
- **WHEN** a model in Steady phase receives any failure reason
- **THEN** its `Phase`, `Cycle` and `MissCount` are unchanged

