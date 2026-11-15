# Actors

All runtime work in njord happens in actors. They come in two hosting shapes:

| Shape | Lifetime | Naming |
|---|---|---|
| Cluster singleton | One instance, BackoffOnFailure supervised | `*Manager`, `*Gateway`, `*Pipeline`, `*Tracker`, `*Consumer` |
| Shard region entity | One instance per entity ID (location), persistent | `*Worker` |

## Topology

```
SINGLETONS (Cluster Singleton)
+-- PollManager (non-persistent, IWithTimers)                [Njord.Polling]
|   Starts one PollWorker per location, receives PollWorkerReports
+-- OpenMeteoGateway (non-persistent)                        [Njord.OpenMeteo]
|   Stream-based: queue -> BudgetGate -> 6 typed clients -> SinkRef to pipeline
+-- ForecastPipeline (non-persistent)                        [Njord.Pipeline]
|   MergeHub -> FeatureEngine (89 rules) -> BroadcastHub
+-- BudgetTracker (persistent)                               [Njord.Budget]
|   Per-minute, per-day, per-month usage accounting
+-- GrpcConsumer (non-persistent)                            [Njord.Grpc]
    Entity state, BroadcastHub of EntityEvents, availability tracking

SHARD REGIONS (Sharded Entity)
+-- PollWorker (persistent, entity = location, no passivation)  [Njord.Polling]
    Per-domain timers, discovery/steady cycle, FetchDomain dispatch
```

<likec4-view view-id="polling"></likec4-view>

## Actor details

| Actor | Project | Persistent | Hosts |
|-------|---------|-----------|-------|
| PollManager | Njord.Polling | no | Singleton, IWithTimers |
| PollWorker | Njord.Polling | yes | Shard region (entity = location name) |
| OpenMeteoGateway | Njord.OpenMeteo | no | Singleton, internal Akka.Streams pipeline |
| ForecastPipeline | Njord.Pipeline | no | Singleton, MergeHub + BroadcastHub |
| BudgetTracker | Njord.Budget | yes | Singleton |
| GrpcConsumer | Njord.Grpc | no | Singleton, SourceRef consumer |

All actors are `sealed`. All use `BackoffOnFailure` supervision.

## Addressing

Actors are never passed as constructor parameters. They are resolved at runtime by marker interface:

```csharp
Context.TryGetActor<IPollManager>(out var manager);
```

Markers live in `Njord.Core/ActorKeys.cs`. Each library registers its actors via `services.AddActorRegistration<T>()`.

Messages for the PollWorker shard carry `IWithLocationId` for routing through the `ShardMessageExtractor`.

## State pattern

A stateful actor has two files:

- `<Actor>.cs` - plumbing: message routing, persistence lifecycle, timers
- `<Actor>State.cs` - immutable record with `Empty` static, `Apply(event)`, `ProcessCommand`

State is never sent raw. Callers get message records; `SaveSnapshot` gets `Persisted<Name>State`. State extensions live next to the state record, not inside it.

## Persistence

Persistent actors write events to the Akka.Persistence journal (SQLite by default, PostgreSQL optional). Records live in `Njord.Persistence` (extend-only, positional records, `DateTimeOffset`/`TimeSpan`). Snapshot when `LastSequenceNr % SnapshotInterval == 0`; after `SaveSnapshotSuccess`: `DeleteMessages(seqNr)` + `DeleteSnapshots(seqNr - 1)`.
