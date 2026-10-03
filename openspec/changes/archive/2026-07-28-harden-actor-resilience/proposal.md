## Why

Seven verified resilience defects exist across the actor system, all following
the same pattern: a correct implementation exists somewhere in the codebase but
was not applied consistently. Left unaddressed, these cause unbounded journal
growth (SchedulerActor), silent data loss (fire-and-forget OfferAsync, missing
HistoryResult DTO mapping), and stale state after actor restarts
(GrpcSnapshotConsumerActor, DiscoveryActor ignoring Terminated). None are
visible to users today because conditions haven't been triggered — but each
becomes more likely as uptime increases or configuration grows.

## What Changes

- **SchedulerActor snapshotting**: Add snapshot/journal-cleanup lifecycle
  (SaveSnapshot → DeleteMessages → DeleteSnapshots) matching the pattern already
  used by BudgetTrackerActor, ForecastHistoryActor, and all snapshot actors.
- **SchedulerActor OfferAsync handling**: Pipe the `OfferAsync` result in the
  Ready state to detect and log queue-full/dropped polls, matching the existing
  Connecting-state pattern.
- **PipelineActor failure propagation**: Replace `null!` failure returns in
  PipeTo with `Status.Failure(ex)`, matching EgressActor's existing pattern.
- **MqttConnectionActor sink failure propagation**: Replace the `null`-returning
  `ContinueWith` with proper `Status.Failure` propagation.
- **EnrichmentSnapshotDtos HistoryResult mapping**: Add the missing
  `HistoryResult` entry to the `EnrichmentTypes` dictionary so history
  enrichment data survives actor restarts.
- **GrpcSnapshotConsumerActor Terminated handling**: Replace the no-op
  `Receive<Terminated>` with proper re-request logic, matching
  ModelStateActor/EnrichmentActor/MqttEgressActor.
- **DiscoveryActor upstream watching**: Add `Context.Watch()` for upstream
  actors and handle `Terminated` to re-request refs, matching MqttEgressActor.

## Non-goals

- Changing supervision strategies or adding backoff (separate change).
- Replacing blanket `Directive.Resume` with typed deciders (separate change).
- Adding new test coverage for MqttEgressActor or gRPC streaming (separate
  change).
- Changing the `|` key separator convention in actor state dictionaries.
- Any polling frequency or API budget changes (zero budget impact — this change
  is purely internal resilience).

## Capabilities

### New Capabilities

_None — all changes harden existing behavior without introducing new features._

### Modified Capabilities

- `poll-scheduler`: SchedulerActor gains snapshotting and OfferAsync result
  handling for resilience.
- `pipeline-actor`: PipelineActor failure paths propagate Status.Failure instead
  of null.
- `mqtt-actor-topology`: MqttConnectionActor propagates sink-creation failures;
  DiscoveryActor watches upstream actors and handles Terminated.
- `snapshot-persistence-dtos`: EnrichmentSnapshotDtos gains HistoryResult type
  mapping.
- `snapshot-actors`: GrpcSnapshotConsumerActor handles Terminated by
  re-requesting refs.

## Impact

- **Files modified**: SchedulerActor.cs, SchedulerDtos.cs, PipelineActor.cs,
  MqttConnectionActor.cs, EnrichmentSnapshotDtos.cs,
  GrpcSnapshotConsumerActor.cs, DiscoveryActor.cs
- **Persistence**: New SchedulerSnapshotDto added to Njord.Persistence. Existing
  journals remain compatible (snapshots are additive — recovery still works
  without a snapshot, it just replays more events).
- **APIs**: No public API changes.
- **Dependencies**: No new packages.
- **Risk**: Low — each fix applies an existing, tested pattern from elsewhere in
  the codebase. All changes are backward-compatible with existing persisted data.
