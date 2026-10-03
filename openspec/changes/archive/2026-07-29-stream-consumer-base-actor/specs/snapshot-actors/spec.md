# snapshot-actors Delta Specification

## MODIFIED Requirements

### Requirement: SnapshotConsumerActor routes events from BroadcastHub to snapshot actors
`SnapshotConsumerActor` SHALL subscribe to the EgressActor BroadcastHub. It SHALL route `PerModelUpdate` events to `ForecastSnapshotActor` via Ask and wait for Ack. It SHALL route `EnrichmentUpdate` events to `EnrichmentSnapshotActor` via Ask/Ack.

The SnapshotConsumerActor SHALL maintain a `HashSet<IActorRef>` of explicitly tracked dependencies (`_watchedDeps`). It SHALL call `Context.Watch` and add to the set only for refs resolved via `GetActorAsync`. On `Terminated`, it SHALL ignore any ref not in the tracked set.

The SnapshotConsumerActor SHALL detect dead refs returned by `GetActorAsync` (ref matches `_lastTerminatedRef`) and schedule a retry with exponential backoff (`min(1s × 2^retryCount, 30s)`) instead of immediately re-resolving. It SHALL gate its transition to the next phase with `_lastTerminatedRef is not null` to prevent stale in-flight responses from triggering premature transitions.

The SnapshotConsumerActor SHALL wire a `SharedKillSwitch.Flow<EgressEvent>()` into its stream graph. On `Terminated` for a tracked dependency, it SHALL call `_killSwitch.Shutdown()` before re-resolving.

#### Scenario: Forecast update routed with backpressure
- **WHEN** a PerModelUpdate event arrives from the BroadcastHub
- **THEN** it is sent to ForecastSnapshotActor via Ask and Ack is awaited

#### Scenario: Enrichment update routed with backpressure
- **WHEN** an EnrichmentUpdate event arrives from the BroadcastHub
- **THEN** it is sent to EnrichmentSnapshotActor via Ask and Ack is awaited

#### Scenario: EgressActor restart triggers re-subscription
- **WHEN** SnapshotConsumerActor receives Terminated for the EgressActor
- **THEN** it shuts down the KillSwitch and re-requests a SourceRef with backoff retry
- **THEN** it transitions to WaitingForSource and rematerializes the stream graph

#### Scenario: SnapshotConsumerActor remains functional after upstream restart
- **WHEN** the EgressActor restarts and SnapshotConsumerActor re-subscribes
- **THEN** new events from the BroadcastHub are routed to snapshot actors

#### Scenario: Dead ref detected triggers backoff retry
- **WHEN** `GetActorAsync<EgressActor>` returns the same dead ref from the registry
- **THEN** the actor schedules a retry with exponential backoff instead of tight-looping

#### Scenario: Untracked Terminated is ignored
- **WHEN** `Terminated` arrives for a ref not in `_watchedDeps` (e.g. StreamSupervisor child)
- **THEN** the actor ignores it
