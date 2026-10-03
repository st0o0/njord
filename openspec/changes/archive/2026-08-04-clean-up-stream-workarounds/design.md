# Design: Clean Up Stream Workarounds

## A — GrpcSnapshotConsumerActor → StreamConsumerActor

The actor currently duplicates ~60 lines of retry/kill-switch/watch logic from `StreamConsumerActor`. The extra complexity is the two-phase resolution: first EgressActor (for SourceRef), then ForecastSnapshotActor + EnrichmentSnapshotActor.

**Solution:** Extend `StreamConsumerActor`. Override `ResolveDependencies` to resolve all three actors. Override `ConfigureWaitingForRefs` to handle the three resolve responses and call `TryTransition` when all are set. Override `MaterializeGraph(killSwitch)` with the existing graph, replacing the `is var _` trick with a normal async body.

The `is var _` trick becomes unnecessary because the method body is no longer a switch expression — it's a regular async block with `await` + `return`.

## B — gRPC Stream Cancellation via KillSwitch

Current `TakeWhile(_ => !ct.IsCancellationRequested)` only fires between elements. If the BroadcastHub source is idle, the stream doesn't terminate until the next element.

**Solution:** Create a `SharedKillSwitch` per RPC call. Register the CancellationToken:
```csharp
var ks = KillSwitches.Shared($"grpc-{context.Peer}");
context.CancellationToken.Register(() => ks.Shutdown());
```
Insert `.Via(ks.Flow<T>())` in place of the `TakeWhile`. The kill-switch terminates the stream immediately on disconnect, regardless of source activity.

Keep `context.CancellationToken` on `WriteAsync` as defense-in-depth.

## C — Narrow Bare Catch in EnrichmentActor

Replace `catch { log.Warning("...timed out...") }` with:
```csharp
catch (AskTimeoutException)
{
    log.Warning("SensorHub Ask timed out for {Location}", consensus.Location);
}
```

Let other exceptions propagate — the stream supervision decider (Resume on transient, Stop on unknown) handles them correctly.

## D — Mutable Closures → Scan

Three pipelines capture mutable locals in closures. Replace with `Scan` which makes state explicit and thread-safe by construction.

### EnrichmentActor (previous ConsensusSnapshot)
`ConsensusSnapshot? previous = null` captured in SelectAsync closure → `Scan` with `(Previous, Current)` tuple, then SelectAsync on the pair.

### ModelStateActor (knownCapabilities)
`Dictionary<(string, string), HashSet<ParameterDef>> knownCapabilities` → `Scan` accumulating capabilities, emitting `(FetchOutcome.Success, KnownCapabilities)` pairs.

### MqttEgressActor (lastPublished)
`Dictionary<string, int> lastPublished` dedup cache → `Scan` accumulating hash state, then SelectMany on the scanned pair.

**Note:** SchedulerActor (line 218) captures no mutable state in the stream itself — the `self` capture is an IActorRef (immutable, thread-safe). No change needed there.

## E — Remove Empty PostStop

Delete the empty `PostStop()` overrides in PipelineActor, SchedulerActor, and GrpcSnapshotConsumerActor. They add nothing over the base class call.

## F — Verification

Run full test suite, slopwatch, and dotnet format to confirm no regressions.
