## REMOVED Requirements

### Requirement: Manual refresh commands bypass the schedule
**Reason**: `RefreshModel` and `RefreshLocation` commands have no producer — no
code in the system sends them. They are pre-built extension points for a feature
that does not exist.
**Migration**: Re-add when a manual-refresh trigger (API endpoint, MQTT command,
etc.) is implemented.

## MODIFIED Requirements

### Requirement: Hash results from the pipeline update the schedule
The SchedulerActor SHALL handle `HashResult(Location, ModelId, Hash)` messages
from the pipeline's Ask flow. On receipt, the actor SHALL compare the hash with
`lastHash`. If changed: persist a `DataChanged` event, update
`lastChangeUtc`/`prevChangeUtc`, reset `missCount`, and schedule the next poll.
If unchanged: increment `missCount` and schedule retry. The actor SHALL respond
with `Ack` after processing.

Additionally, the SchedulerActor SHALL consume `FetchOutcome.Failure` from its
BroadcastHub consumer and route to reason-specific retry logic (see
failure-routing spec).

#### Scenario: Changed hash triggers persist and reschedule
- **WHEN** a `HashResult` arrives with a hash different from `lastHash`
- **THEN** a `DataChanged` event is persisted, the state is updated, and `Ack` is returned

#### Scenario: Unchanged hash increments miss count
- **WHEN** a `HashResult` arrives with a hash equal to `lastHash`
- **THEN** `missCount` is incremented, next retry is scheduled, and `Ack` is returned

#### Scenario: Failure from BroadcastHub triggers reason-based retry
- **WHEN** a `FetchOutcome.Failure(Transport)` is consumed from the BroadcastHub
- **THEN** the scheduler increments missCount and schedules a backoff retry
