## MODIFIED Requirements

### Requirement: State is persisted and recovered via Akka.Persistence
The SchedulerActor SHALL persist `DataChanged` events to a SQLite journal via Akka.Persistence. On recovery, the actor SHALL rebuild all `ModelPollState` entries from the event stream. If a recovered `nextPollUtc` is in the past, the actor SHALL poll immediately. If a cycle is known from recovery, the actor SHALL enter Steady phase directly without re-discovery.

The SchedulerActor SHALL save a snapshot of its full `_states` dictionary every 50 persisted events. The snapshot SHALL be a dedicated `SchedulerSnapshotDto` containing all `ModelPollState` entries. On `SaveSnapshotSuccess`, the actor SHALL delete all journal entries up to the snapshot's sequence number and delete all previous snapshots. On `SaveSnapshotFailure`, the actor SHALL log a warning. Recovery SHALL prefer the latest snapshot and replay only events after it.

#### Scenario: Recovery skips discovery for known cycles
- **WHEN** SchedulerActor recovers with persisted events containing learned cycles
- **THEN** it enters Steady phase for those models without re-discovery

#### Scenario: Past nextPollUtc triggers immediate poll
- **WHEN** SchedulerActor recovers and a model's nextPollUtc is in the past
- **THEN** it polls that model immediately

#### Scenario: Recovery with no prior events starts in Discovery
- **WHEN** SchedulerActor recovers with no persisted events and no snapshot
- **THEN** all models start in Discovery phase

#### Scenario: Snapshot saved after 50 persisted events
- **WHEN** 50 DataChanged events have been persisted since the last snapshot
- **THEN** the actor saves a snapshot of the full _states dictionary

#### Scenario: Journal and old snapshots cleaned after snapshot success
- **WHEN** a snapshot save succeeds
- **THEN** journal entries up to the snapshot sequence number are deleted
- **THEN** all previous snapshots are deleted

#### Scenario: Snapshot failure is logged without crash
- **WHEN** a snapshot save fails
- **THEN** the actor logs a warning and continues operating

#### Scenario: Recovery from snapshot plus events
- **WHEN** SchedulerActor recovers with both a snapshot and subsequent events
- **THEN** the snapshot is restored first, then remaining events are replayed

## ADDED Requirements

### Requirement: OfferAsync result is handled in the Ready state
The SchedulerActor SHALL handle the result of `OfferAsync` in the Ready state by piping it to Self. If the offer fails (queue completed or dropped), the actor SHALL log a warning and re-schedule the poll for that (location, model) pair using the standard `ScheduleNext` logic. The actor SHALL NOT silently discard a failed offer.

#### Scenario: Successful offer in Ready state
- **WHEN** a ScheduledPoll fires in the Ready state and OfferAsync succeeds
- **THEN** the target is enqueued and no additional action is taken

#### Scenario: Failed offer in Ready state triggers re-schedule
- **WHEN** a ScheduledPoll fires in the Ready state and OfferAsync fails
- **THEN** the actor logs a warning with the location, model, and error
- **THEN** the poll is re-scheduled via ScheduleNext
