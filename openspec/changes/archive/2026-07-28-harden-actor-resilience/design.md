## Context

The njord actor system has seven verified resilience gaps where a correct
pattern exists in one actor but was not applied consistently to others. All
fixes are mechanical — applying an existing, tested pattern to the actors that
missed it. No new architectural patterns are introduced.

Current state of persistence lifecycle across actors:

| Actor | Events | Snapshots | Cleanup | Status |
|---|---|---|---|---|
| BudgetTrackerActor | ✅ | ✅ (50) | ✅ | Correct |
| ForecastHistoryActor | ✅ | ✅ (configurable) | ✅ | Correct |
| ForecastSnapshotActor | — | ✅ (20) | ✅ | Correct |
| EnrichmentSnapshotActor | — | ✅ (14) | ✅ | Correct |
| SchedulerActor | ✅ | ❌ | ❌ | **Unbounded** |

Current state of Terminated handling across stream-consuming actors:

| Actor | Watches upstream | Handles Terminated | Status |
|---|---|---|---|
| ModelStateActor | ✅ | ✅ re-requests refs | Correct |
| EnrichmentActor | ✅ | ✅ re-requests refs | Correct |
| MqttEgressActor | ✅ | ✅ re-requests refs | Correct |
| GrpcSnapshotConsumerActor | ✅ | ❌ no-op handler | **Stale** |
| DiscoveryActor | ❌ | ❌ no handler | **Stale** |

## Goals / Non-Goals

**Goals:**

- Bound SchedulerActor journal growth via snapshotting + cleanup.
- Make OfferAsync failures in SchedulerActor visible and retriable.
- Propagate stream-ref materialization failures as `Status.Failure` so consumers
  can detect and recover instead of hanging.
- Ensure all enrichment result types survive snapshot recovery.
- Make all stream-consuming actors handle upstream restarts consistently.

**Non-Goals:**

- Changing supervision strategies or adding backoff supervisors.
- Replacing blanket `Directive.Resume` with typed deciders.
- Adding new test suites for currently untested actors.
- Modifying config validation or startup ordering.

## Decisions

### D1: SchedulerActor snapshot strategy — mirror BudgetTrackerActor

**Decision:** Add `SchedulerSnapshotDto` containing the full `_states` dictionary
and snapshot every 50 persisted events (matching BudgetTrackerActor's interval).
On `SaveSnapshotSuccess`, delete journal entries up to that sequence number and
delete previous snapshots.

**Why 50, not 20:** SchedulerActor persists `DataChanged` events which are
small and fast to replay. 50 events is roughly 2 days of data at typical
polling rates — a reasonable recovery window. The snapshot itself is larger
(full state dictionary) so snapshotting less frequently is the better trade.

**Alternatives considered:**
- Snapshot every 10 events — too frequent, snapshot writes dominate.
- No snapshot, just periodic DeleteMessages — loses ability to recover state.

**Recovery compatibility:** Existing journals without snapshots continue to work.
`Recover<SnapshotOffer>` is additive — if no snapshot exists, the actor replays
all events as before, then starts creating snapshots going forward.

### D2: OfferAsync — PipeTo pattern matching Connecting state

**Decision:** In `OnScheduledPoll` (Ready state), pipe `OfferAsync` result to
Self as either a no-op success or an `OfferFailed` message that logs and
re-schedules the poll.

**Why not await:** OfferAsync returns `IQueueOfferResult`, not a business
message. Using `PipeTo` keeps the actor single-threaded and matches the
existing Connecting-state pattern at line 129.

### D3: Status.Failure instead of null! in PipeTo failure handlers

**Decision:** Replace `return null!` (PipelineActor) and `return null`
(MqttConnectionActor) with `new Status.Failure(ex)`. The consuming actors
already handle `Status.Failure` via Akka's built-in dead-letter mechanism,
or we add explicit handlers where needed.

**Why Status.Failure:** It's the Akka convention for communicating async
failures between actors. EgressActor already uses this pattern correctly.
The consuming actors (SchedulerActor, MqttEgressActor, DiscoveryActor) will
either need to handle `Status.Failure` explicitly or let it dead-letter with
a log message — both are better than silent hang.

### D4: HistoryResult — add to EnrichmentTypes dictionary

**Decision:** One-line addition to `EnrichmentSnapshotDtos.cs`. No structural
change needed — the dictionary is the sole extensibility point.

### D5: GrpcSnapshotConsumerActor — full Terminated handling

**Decision:** Replace the no-op `Receive<Terminated>` with the same pattern
used by ModelStateActor: null out refs, re-request from EgressActor, transition
to WaitingForSource.

### D6: DiscoveryActor — add Watch + Terminated handling

**Decision:** Add `Context.Watch()` calls in PreStart for both
MqttConnectionActor and EgressActor. Add Terminated handler that nulls stale
refs and re-requests, matching MqttEgressActor's existing pattern.

## Risks / Trade-offs

- **[SchedulerActor snapshot size]** The snapshot contains the full `_states`
  dictionary (one entry per location×model). At typical config sizes (5×6=30
  entries) this is trivially small. → No mitigation needed unless config grows
  to hundreds of entries.

- **[OfferAsync retry loop]** If the queue is persistently full, the OfferFailed
  → re-schedule → OfferFailed cycle could create log noise. → Mitigated by the
  existing backpressure: the queue has capacity 16 with `OverflowStrategy
  .Backpressure`, so OfferAsync will await capacity rather than fail immediately.
  True failures indicate a broken pipeline, which is worth logging.

- **[Status.Failure in consumers]** Consumers that don't explicitly handle
  `Status.Failure` will get an unhandled-message log (Akka default behavior).
  → Acceptable — an unhandled `Status.Failure` log is far better than silent
  hang. Consumers can add explicit handlers if the log noise matters.

- **[DiscoveryActor re-request on Terminated]** Re-requesting refs after an
  upstream restart may trigger a re-discovery publish. → Acceptable — discovery
  payloads are retained and idempotent.
