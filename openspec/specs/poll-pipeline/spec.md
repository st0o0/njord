# poll-pipeline Specification

## Purpose

Scheduled polling pipeline that fans out forecast fetches over configured locations and models each cycle, throttles to the request budget, aggregates outcomes with a bounded window, survives failures via restart-with-backoff, and logs per-cycle summaries.

## Requirements

### Requirement: Cycles are scheduled at the configured interval
The pipeline SHALL start a poll cycle at the configured interval (default
60 minutes). Each cycle SHALL carry a cycle id derived from the tick timestamp
obtained via `TimeProvider`.

#### Scenario: Cycle id from injected time
- **WHEN** a tick fires at 2026-07-11T12:00:00Z on the injected `TimeProvider`
- **THEN** the resulting cycle id is derived from that timestamp

### Requirement: Each cycle fans out over locations × models
For every cycle the pipeline SHALL issue exactly one fetch request per
configured (location, model) pair.

#### Scenario: Fan-out count
- **WHEN** 2 locations and 4 models are configured and a cycle starts
- **THEN** exactly 8 fetch requests are issued for that cycle

### Requirement: Outbound requests respect the per-minute budget
The pipeline SHALL throttle outbound fetch requests to at most the resolved
per-minute request budget.

#### Scenario: Burst is smoothed
- **WHEN** a cycle fans out more requests than the per-minute budget
- **THEN** requests beyond the budget are delayed into subsequent minutes
  rather than dropped or sent immediately

### Requirement: Cycles aggregate with a timeout and never block on missing models
The pipeline SHALL aggregate fetch outcomes per cycle id and close every cycle
after a bounded aggregation window. The cycle result SHALL contain the
successfully retrieved model forecasts and the list of missing or failed
(location, model) pairs. A missing, slow, or failed model MUST NOT prevent the
cycle result from being emitted, and MUST NOT fail the stream. The pipeline
MUST NOT join model sub-streams with `Zip`-style operators.

#### Scenario: Partial cycle completes
- **WHEN** 3 of 4 model fetches succeed and one times out
- **THEN** a cycle result with 3 forecasts and 1 missing entry is emitted
  within the aggregation window

### Requirement: The pipeline restarts with backoff
The pipeline SHALL be wrapped in restart-with-backoff supervision so that an
unexpected stage failure restarts the pipeline (with exponential backoff and
jitter) without terminating the service.

#### Scenario: Stage failure does not kill the service
- **WHEN** a pipeline stage throws an unhandled exception
- **THEN** the pipeline restarts after the backoff delay and the host process
  keeps running

### Requirement: v1 sink logs cycle summaries
Until consensus and MQTT egress exist, the pipeline SHALL terminate in a sink
that logs one summary per cycle: cycle id, per-model success/failure, and
counts of received vs. missing forecasts.

#### Scenario: Summary per cycle
- **WHEN** a cycle result is emitted
- **THEN** exactly one summary log entry for that cycle id is written
