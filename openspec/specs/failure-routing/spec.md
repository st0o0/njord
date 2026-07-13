# failure-routing Specification

## Purpose

Routing of `FetchOutcome.Failure` messages from the pipeline BroadcastHub to the SchedulerActor, with reason-based retry logic (backoff for transport errors, minimum delay for rate limiting, no retry for permanent failures).

## Requirements

### Requirement: FetchOutcome.Failure carries location and model context
Each `FetchOutcome.Failure` SHALL carry the `Location` (string) and `Model`
(`WeatherModel`) of the failed fetch so downstream consumers can identify
which (location, model) pair failed.

#### Scenario: Transport failure carries target identity
- **WHEN** a fetch for (lucerne, icon_d2) returns HTTP 500
- **THEN** the `FetchOutcome.Failure` carries `Location = "lucerne"` and `Model.Id = "icon_d2"`

#### Scenario: Rate-limited failure carries target identity
- **WHEN** a fetch for (zurich, ecmwf_ifs025) returns HTTP 429
- **THEN** the `FetchOutcome.Failure` carries `Location = "zurich"` and `Model.Id = "ecmwf_ifs025"`

### Requirement: The scheduler consumes failures and retries by reason
The SchedulerActor SHALL consume `FetchOutcome.Failure` from the BroadcastHub
and decide retry behavior based on `FetchFailureReason`:

- **Transport**: treat as a miss -- increment `missCount`, schedule retry with
  the existing exponential backoff (1m, 2m, 4m... capped at 15m).
- **RateLimited**: schedule retry at `max(5 minutes, normal backoff)` and log
  a warning.
- **ModelUnavailable** or **MalformedPayload**: no retry this cycle -- log a
  warning, wait for the next regular scheduled poll.

#### Scenario: Transport failure triggers backoff retry
- **WHEN** a `Failure(Transport)` arrives for (lucerne, icon_d2) with current missCount = 0
- **THEN** missCount becomes 1 and the next retry is scheduled in 1 minute

#### Scenario: Second transport failure doubles backoff
- **WHEN** a `Failure(Transport)` arrives for (lucerne, icon_d2) with current missCount = 1
- **THEN** missCount becomes 2 and the next retry is scheduled in 2 minutes

#### Scenario: Rate-limited failure enforces minimum 5-minute delay
- **WHEN** a `Failure(RateLimited)` arrives for (lucerne, icon_d2) with current missCount = 0
- **THEN** the next retry is scheduled in 5 minutes (not 1 minute)

#### Scenario: ModelUnavailable does not trigger retry
- **WHEN** a `Failure(ModelUnavailable)` arrives for (lucerne, icon_d2)
- **THEN** no immediate retry is scheduled; the next poll occurs at the regular scheduled time

#### Scenario: MalformedPayload does not trigger retry
- **WHEN** a `Failure(MalformedPayload)` arrives for (lucerne, icon_d2)
- **THEN** no immediate retry is scheduled; the next poll occurs at the regular scheduled time
