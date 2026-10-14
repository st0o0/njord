## Context

See proposal.md - Why. Current shape after `stable-pipeline-input`: `ForecastPipeline` (a cluster singleton behind
`BackoffOnFailure`; the registry holds the singleton proxy) materializes `MergeHub → ZipWith(sensors, settings) →
StatefulSelectMany → BroadcastHub(2)` once per incarnation and answers `RequestPipelineOutput` at any time.
`StreamConsumerActor` (src/Njord.Core/Actors/StreamConsumerActor.cs) still runs `WaitingForRefs` / `Ready` with a
stash, tracked dependencies with dead-ref detection, a resolve retry counter, a graph generation, a graph-loss counter
with a stability window and eleven subclass hooks; both subclasses repeat the same handshake code. They watch the
registry ref - the singleton proxy - so a pipeline restart reaches them only through their SourceRef failing.
`MqttStatePublisher` publishes in `SelectAsync(1)` straight off the SourceRef, so a broker that is slow (up to the
10 s publish timeout per message) back-pressures the pipeline's `BroadcastHub` and with it the read model.

Constraints: `Njord.Messages` stays Akka-free; feature libraries do not reference each other; `TimeProvider` for
time decisions (none are added here; the actor timers are scheduler-based like the gateway's attach timers).

## Goals / Non-Goals

**Goals:**
- One attach path for both consumers, in the base, with every request ending (reply, failure or timeout).
- No consumer can slow the pipeline hub.
- A restarted pipeline analyzes with the current settings and sensor map from its next forecast on, not with the startup values until the next change or expiry tick.

**Non-Goals:**
- Changing the pipeline's hubs, the gateway, or the read model's query semantics.

## Decisions

### D1 Consumer base: attach, watch the incarnation, re-attach

`StreamConsumerActor` keeps its name and becomes the base of pipeline-output consumers. The subclass implements one
member, `Task MaterializeGraph(Source<PipelineEvent, NotUsed> events)`, returning the completion of its graph; it may
override `ConsumerName`. The base:

1. **Attach** (on `PreStart` and on every retry): new request id, starts a reply timer (default 10 s), pipes
   `Context.GetActorAsync<IForecastPipeline>()` to itself tagged with the id, then sends `RequestPipelineOutput(id)`
   to the resolved ref. The single timer covers both the registry wait and the reply, so an attach never hangs.
2. **Completed** with the current id: cancels the timer, watches `Sender` (the pipeline *incarnation* that answered,
   not the proxy), creates a `SharedKillSwitch` for this subscription, passes `sourceRef.Source.Via(killSwitch)` to
   `MaterializeGraph` and pipes the completion to itself tagged with the subscription id. Logs Debug
   `Pipeline SourceRef received from {Source}`.
3. **Failed / resolve failure / reply timeout** with the current id: Warning, retry after
   `retryPolicy.DelayWithJitter(attempts++)`.
4. **Lost subscription** - `Terminated` of the watched incarnation, or the completion of the current subscription,
   whichever comes first: unwatch, shut the kill switch, count `njord_stream_consumer_rewires_total{consumer}`,
   Warning, retry after the backoff. The second of the two signals finds no current subscription and is ignored.
5. A subscription that stays up for the stability window (default 60 s) resets `attempts`.

All replies and completions with an old id are ignored. Timers come from `IWithTimers`, so they die with the actor.
The graph completion record implements `IDeadLetterSuppression`: after the actor stops it lands in dead letters
silently.

**Removed, and why:**
- *`WaitingForRefs` / `Ready` and the stash* - the read model's hub now exists from its start (D2) and the queries
  answer from actor state, so nothing must wait for a SourceRef; MQTT has no queries.
- *Tracked-dependency set, `IsDeadRef`, `RetryResolve`, the separate resolve retry count* - they guarded against the
  registry returning a terminated ref. The registry holds the singleton proxy, which outlives every pipeline
  incarnation; the incarnation is learned from the reply. A dead incarnation that still answers is watched and its
  `Terminated` arrives at once, which simply re-attaches.
- *Graph generation counter* - the request id doubles as the subscription id; one counter covers stale replies,
  stale completions, the actor's own kill switch and its stop.
- *Subclass hooks (`ResolveInitialDependencies`, `RequestSourceRefs`, `ResolveDependencies`, `AllRefsReady`,
  `ConfigureWaitingForRefs`, `ConfigureReady`, `OnDependencyLost`, `OnStreamLost`, `TrackDependency`,
  `NextRequestId`, `ScheduleRetryResolve`, `TryTransition`)* - both subclasses had identical code for them; the base
  owns the single SourceRef.
- *Synchronous `Context.GetActor` in `PreStart`* - it crashed the consumer when the pipeline was not yet
  registered; the bounded async resolve retries instead.

**Kept, and why:**
- *Re-attach when the own graph completes or fails* - the safety net for a consumer bug (a mapping stage stopping on
  an unexpected exception) and for a StreamRef protocol failure while the pipeline lives.
- *Backoff that grows with consecutive losses plus the stability window* - without it a graph that dies on its first
  element would re-attach every second forever.
- *Kill switch per subscription* - after `Terminated` the old SourceRef would also fail on its own, but the switch
  guarantees the old graph is gone before the next one runs, so no event is consumed twice.
- *`njord_stream_consumer_rewires_total`* - still the one signal that a consumer lost its subscription. It now
  counts every loss (pipeline termination included): which of `Terminated` and the SourceRef failure arrives first is
  a race, so counting only one of them would make the counter nondeterministic.

*Alternatives:* keep the hooks and only delete the generation logic (rejected: the hooks are the duplicated code);
Ask with `PipeTo` instead of tell + timer (rejected: the reply's `Sender`, which is the incarnation to watch, is lost
in an Ask).

### D2 Read model: hub at start, MergeHub kept

`ForecastReadModel` materializes `MergeHub → BroadcastHub(2)` plus the permanent `Sink.Ignore` drain in `PreStart`.
Each subscription runs `events → update state → WatchTermination → Recover(empty) → mergeSink`. The `MergeHub` is
kept although the pipeline no longer rebuilds: it is what lets the hub exist before the first subscription (so
`RequestReadModelStream` is answered from the start without a stash or a "not materialized yet" failure) and it lets
streaming clients keep their stream across a re-attach at no extra cost. *Alternative:* one `BroadcastHub` per
subscription - same amount of code, but streaming clients end on every re-attach and requests before the first
subscription need a stash or a failure. `RequestReadModelStreamFailed` remains for a failed SourceRef
materialization. The per-client `Buffer(256, DropHead)` is unchanged.

### D3 MQTT private stream

`events → Buffer(MqttEgressTuning.EventBufferSize = 256, DropHead) → map to messages → Sink.ForEachAsync(1, publish)`.
The buffer always demands from the SourceRef and drops the oldest event when full, so the pipeline hub never waits for
the broker. The dedup check and the record of a successful publish stay together in the publish function: with
parallelism 1 the check and the record cannot interleave with another message, and a failed or timed-out publish
records nothing. A separate dedup stage before the sink would check against a cache that the in-flight publish has
not yet updated. The dedup cache is created per `MaterializeGraph`, i.e. per subscription. The mapping stage keeps
the logging decider; a mapping bug ends the graph and the base re-attaches. `SelectAsync(1)` + `Sink.Ignore` becomes
`Sink.ForEachAsync(1, …)`, whose `Task<Done>` is the completion the base watches. 256 events cover several poll
cycles (one forecast yields at most six events).

### D4 Pipeline start fetches current inputs

After materializing its graph, `ForecastPipeline.PreStart` uses `Context.TryGetActor`:
- `IConfigManager` → `QueryAnalysisSettings`; `ConfigManager` replies `AnalysisSettingsResult(current)`
  (hierarchy base `AnalysisSettingsResponse`, `Njord.Messages/Config`). The pipeline forwards it to the settings
  side channel like `AnalysisSettingsChanged`.
- `ISensorHub` → `RequestSensorSnapshots` (`Njord.Messages/Sensors`); the SensorHub replies its current
  `SensorSnapshots` to the sender, which the pipeline already handles.

Not registered → Debug log, startup settings / empty map stay. The pipeline does not wait for either answer, so
nothing can block it; there is nothing to time out - a lost answer leaves exactly today's behavior until the next
change or expiry tick. Order: `ConfigManager` replies with its current value and sends later changes afterwards, so a
reply never overtakes a newer change. *Alternatives:* Ask with timeout in `PreStart` (rejected: blocks or needs a
waiting phase); let the SensorHub and ConfigManager watch the pipeline and push on restart (rejected: they would have
to watch the incarnation behind the proxy, which they never see).

### D5 No drain on the pipeline output hub

The concept sketch shows `BroadcastHub (+ permanent drain)`. With D2 and D3 every consumer drains fast, so a drain
adds no protection against slow consumers. Without any consumer (startup, before the read model attached) the hub
back-pressures and the input buffers (gateway branch 16 + MergeHub 16) hold the first forecasts; a drain would drop
them, and the read model would answer `NotAvailable` for a whole poll interval. The drain is therefore left out.

### D6 `Ack` removed

`Njord.Messages/Common/SharedMessages.cs` (only `Ack`) and the `Common/` folder go; five PollScheduler specs import
the namespace without using it.

## Risks / Trade-offs

- [Events emitted while a consumer re-attaches are lost for it] → accepted (live stream); the next cycle refills.
- [A broker slower than the event rate loses the oldest buffered events] → accepted by the user; dedup resets only
  per subscription, so a dropped event's topic is republished with the next cycle's payload.
- [A `RequestPipelineOutput` sent to the proxy while the pipeline restarts goes to dead letters] → the reply timer
  retries with backoff; the pipeline backoff (3-30 s) and the consumer backoff (1-30 s) converge within seconds.
- [The first forecast after a restart can still see the startup settings and the empty map] → the zip of the side
  channels holds one element per channel from its first pull, so - exactly as for `AnalysisSettingsChanged` and a
  sensor push today - a new value applies from the next forecast on. Before this change the startup values applied
  until the next change or expiry tick; now at most one forecast is affected. Seeding nothing and waiting for the
  answers (with a fallback timer) would remove that forecast too, but would delay every forecast after a start and
  contradict "a late push never delays a forecast"; not worth it for a restart, which only a bug causes.
- [A pushed sensor map in flight can arrive after the start reply] → both describe the same readings; at worst a
  reading that turned stale in between stays until the next expiry tick (≤ 60 s).

## Migration Plan

Internal protocol change only (no config, wire format or persistence). Rollback is a revert of the change's commits.
