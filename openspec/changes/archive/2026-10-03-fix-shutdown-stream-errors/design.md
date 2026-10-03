## Context

Root cause in code:

- `EgressActor.PreStart` pre-materializes a BroadcastHub and a MergeHub with
  `Context.Materializer()` and runs `mergeHubSource.Log("egress-hub").To(broadcastHubSink)`.
- `PipelineActor.MaterializePipeline` runs
  `MergeHub.Source ... BudgetThrottleStage ... Log("pipeline-fetch-in") ...
  SelectAsyncUnordered ... Log("pipeline-fetch-out") ... Buffer ... To(BroadcastHub)`
  plus a local hash consumer.
- Neither graph has a kill switch or a completion handle. On SIGTERM the host stops the
  ActorSystem (CoordinatedShutdown default phases, no Njord-specific task; no
  `ShutdownTimeout`/phase config in `NjordActorSystemSetup`). The actors' stream
  supervisors are children of the actors and die with them, so the Log stages' processor
  actors see `AbruptTerminationException` and log at error level.
- `StreamConsumerActor` (Njord.Core) already uses a `SharedKillSwitch`, but only to
  protect against dependency termination, not for shutdown. `WeatherGrpcService` uses a
  per-call `SharedKillSwitch` tied to the call.
- Note: `PostStop` is too late. By then the actor's children (the stream supervisor and
  its processors) are already stopped, and `KillSwitch.Shutdown()` is asynchronous.
  The kill switch must fire while the actor is still alive.

## Goals / Non-Goals

**Goals:** zero `AbruptTerminationException` at SIGTERM; bounded shutdown time.
**Non-goals:** see proposal (no topology/buffer/normal-operation changes).

## Decision

Alternative (a), refined: actor-owned `UniqueKillSwitch` plus a CoordinatedShutdown task.

1. In each actor, insert `.ViaMaterialized(KillSwitches.Single<T>(), Keep.Right)` right
   after the MergeHub source (via the materialized-value tuple, keeping the existing
   topology), and keep the graph completion `Task` (`Run` with `Sink` materialized value
   or `WatchTermination`).
2. New command `StopStreams` (VerbNoun, co-located with the actor) answered by
   `StreamsStopped`. On receipt the actor calls `killSwitch.Shutdown()`, then
   `PipeTo(sender, success: _ => new StreamsStopped(), failure: ex => new StreamsStopFailed(ex))`
   on `Task.WhenAll(completions)`. Stash/handle it in both `Initializing` and `Ready`
   states of `PipelineActor`.
3. Host: in `NjordActorSystemSetup`, `CoordinatedShutdown.Get(system).AddTask(
   CoordinatedShutdown.PhaseBeforeServiceUnbind, "stop-njord-streams", ...)` resolves
   `IRequiredActor<PipelineActor>` then `EgressActor`, and `Ask`s each in order
   (Pipeline first so no new outcomes reach Egress), with a 5 s timeout each; timeout or
   failure logs a warning and returns `Done` so shutdown is never blocked.

Normal completion propagates downstream: the BroadcastHub sinks complete and consumers
receive completion, which `StreamConsumerActor` already tolerates (actors are stopping).

## Alternatives

- (b) Complete the MergeHub producers first (complete the SinkRefs). MergeHub completes
  only when all producers complete and cannot be completed by its owner; it depends on the
  remote producers (Scheduler, ModelStateActor) cooperating and ordering them. Fragile,
  rejected.
- (c) Lower the log level or filter `AbruptTerminationException` in the supervision
  decider. Cosmetic: the streams are still killed mid-flight (an in-flight
  `SelectAsyncUnordered` fetch is dropped), and the filter would also hide genuine abrupt
  terminations at runtime. Rejected.
- (d) Leave as is. Harmless in effect but pollutes error logs and masks real failures;
  rejected given the small cost of (a).
- (a) with only `PostStop` shutdown: does not work, see Context.

## Risks / Trade-offs

- Shutdown ordering: Mqtt offline message is sent in `MqttConnectionActor.PostStop`;
  stopping Egress streams earlier must not drop it. Mitigation: the MQTT graph is not
  touched; verify in the smoke check that the `offline` message is still published.
- A stuck fetch delays shutdown: bounded by the 5 s ask timeout, then a warning.
- Spec relaxation of "no manual KillSwitch" is intentional and scoped to shutdown.
