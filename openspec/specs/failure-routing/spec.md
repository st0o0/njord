# failure-routing Specification

## Purpose

Routing of failed fetches from the `OpenMeteoGateway` back to the poll worker that asked: failures carry their location and model, the worker attributes every reply to its own configured target regardless of spelling, and it retries every failure and rejection reason with a reason-specific backoff.
## Requirements
### Requirement: Fetch failures carry location and model context
Each `PollOutcome.Failure` returned by `IOpenMeteoClient.FetchAsync` SHALL carry the `Location` (string), `Model` (`WeatherModel`), `Reason` (`PollFailureReason`) and a `Detail` text of the failed fetch. The gateway SHALL forward it to the requester as `FetchForecastFailed(LocationId Location, string ModelId, PollFailureReason Reason, string? Detail)`, so the failure can be identified by (location, model) in logs and replies; the poll worker that asked attributes it to its own target.

#### Scenario: Transport failure carries target identity
- **WHEN** a fetch for (lucerne, icon_d2) returns HTTP 500
- **THEN** the poll worker of (lucerne, icon_d2) receives `FetchForecastFailed` with location `lucerne`, model id `icon_d2` and reason `Transport`

#### Scenario: Rate-limited failure carries target identity
- **WHEN** a fetch for (zurich, ecmwf_ifs025) returns HTTP 429
- **THEN** the poll worker of (zurich, ecmwf_ifs025) receives `FetchForecastFailed` with location `zurich`, model id `ecmwf_ifs025` and reason `RateLimited`

### Requirement: The poll worker retries failed fetches by reason
The poll worker SHALL handle `FetchForecastFailed` and `FetchForecastRejected` by reason, without touching `MissCount`, `Phase` or the learned `Cycle`, persisting the resulting state:

- **Transport**, **ModelUnavailable** and **MalformedPayload** failures: record a transient failure (`TransientFailureCount + 1`) and schedule a retry after the transient backoff (1 min, 2 min, 4 min, 8 min; from the fifth consecutive transient failure on, the discovery interval), logging a warning.
- **RateLimited** failures and **Budget** rejections: record a transient failure and schedule the retry no earlier than 5 minutes from now, logging a warning.
- **QueueFull** and **Expired** rejections: record a transient failure and schedule a retry after the transient backoff, logging a warning.
- **PipelineUnavailable** rejections (the gateway is still attaching to the pipeline input, e.g. right after startup): schedule a retry 5 seconds from now without recording a transient failure, logging a warning.

A fetch request the gateway does not answer within the fetch reply timeout SHALL be handled as a `Transport` failure, so every request ends in a reply or a retry. Every failure and rejection SHALL be reported to the poll manager as a failed outcome.

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
- **WHEN** a target in Steady phase receives any failure reason
- **THEN** its `Phase`, `Cycle` and `MissCount` are unchanged

#### Scenario: Budget rejection backs off at least 5 minutes
- **WHEN** a `Budget` rejection arrives for (lucerne, icon_d2) with `TransientFailureCount = 0`
- **THEN** the next poll is scheduled no earlier than 5 minutes from now

#### Scenario: Queue-full rejection is retried with transient backoff
- **WHEN** a `QueueFull` rejection arrives for (lucerne, icon_d2) with `TransientFailureCount = 0`
- **THEN** the next retry is scheduled in 1 minute

#### Scenario: Pipeline-unavailable rejection retries soon
- **WHEN** a `PipelineUnavailable` rejection arrives for (lucerne, icon_d2) with `TransientFailureCount = 1`
- **THEN** the next poll is scheduled 5 seconds from now and `TransientFailureCount` stays 1

#### Scenario: Unanswered request is retried
- **WHEN** the gateway does not answer a fetch request within the fetch reply timeout
- **THEN** the worker records a transient failure for its target and schedules a backoff retry

### Requirement: Fetch replies are attributed to the configured target
A poll worker SHALL attribute every `FetchForecastCompleted`, `FetchForecastFailed` and `FetchForecastRejected` that answers its own request to its own target, whatever spelling the reply carries, and SHALL update, persist, schedule and report that target under its configured spelling. The poll manager SHALL accept reports only for configured targets: a report for a target that is not configured SHALL be logged at Warning and SHALL NOT create or update a poll state entry or count in a poll cycle.

#### Scenario: Reply spelled differently updates the configured target
- **WHEN** `lucerne` with model `icon_d2` is configured and its worker receives `FetchForecastCompleted(LUCERNE, "ICON_D2", hash)`
- **THEN** the poll state of (lucerne, icon_d2) is updated and no second target appears in the poll states

#### Scenario: Reply for an unknown target is ignored
- **WHEN** the manager receives a report for (atlantis, icon_d2) and `atlantis` is not configured
- **THEN** a warning is logged and the poll states are unchanged

