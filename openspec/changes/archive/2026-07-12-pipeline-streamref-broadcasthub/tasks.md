## 1. Message Protocol

- [x] 1.1 Add new message types in `src/Njord/Pipeline/PipelineMessages.cs`: `RequestPipelineSink`, `PipelineSinkResponse(ISinkRef<WeightedTarget>)`, `RequestPipelineSource`, `PipelineSourceResponse(ISourceRef<FetchOutcome.Success>)`. Remove `RequestPipelineQueue` and `PipelineQueueResponse` from `src/Njord/Pipeline/SchedulerMessages.cs`. Remove `RequestEgressSink` and `EgressSinkResponse` from `src/Njord/Egress/EgressMessages.cs`.
- [x] 1.2 Write `PipelineMessagesSpec` in `src/Njord.Tests/Pipeline/PipelineMessagesSpec.cs` — verify the new message records are constructible and carry the expected StreamRef types.

## 2. PipelineActor — MergeHub + BroadcastHub

- [x] 2.1 Refactor `PipelineActor.MaterializePipeline()` in `src/Njord/Pipeline/PipelineActor.cs`: replace `Source.Queue<WeightedTarget>` entry with `MergeHub.Source<WeightedTarget>` (PreMaterialize to obtain hubSink). Replace the terminal side-effecting `.Select()` + `Ask` + `Sink.Ignore` with `BroadcastHub.Sink<FetchOutcome.Success>` (PreMaterialize to obtain hubSource). Remove the `egressMat` secondary queue, the `ConcurrentDictionary<..., string> lastPublished`, and all `StatePayloadBuilder` usage.
- [x] 2.2 Materialize the feedback consumer locally in `PipelineActor`: `broadcastHubSource.Select(ComputeHash).Ask<Ack>(schedulerActor, 5s).To(Sink.Ignore).Run(mat)`.
- [x] 2.3 Materialize a `SinkRef<WeightedTarget>` from the MergeHub hubSink. Materialize a `SourceRef<FetchOutcome.Success>` from the BroadcastHub hubSource. Store both refs.
- [x] 2.4 Change `PipelineActor.PreStart()`: remove the `RequestEgressSink` call and the stash-until-SinkRef logic. The actor materializes the pipeline immediately and transitions to `Ready`.
- [x] 2.5 Replace `RequestPipelineQueue` handler with `RequestPipelineSink` (responds with stored SinkRef) and add `RequestPipelineSource` handler (responds with stored SourceRef).
- [x] 2.6 Update the `Terminated` handler for the egress actor: the pipeline no longer needs to rematerialize when egress restarts — the BroadcastHub continues running. Remove graph teardown logic for egress restart.
- [x] 2.7 Write `PipelineActorStreamRefSpec` in `src/Njord.Tests/Pipeline/PipelineActorStreamRefSpec.cs` — test: actor materializes without egress dependency; `RequestPipelineSink` returns a usable `SinkRef`; `RequestPipelineSource` returns a usable `SourceRef`; feedback consumer sends `HashResult` via Ask and receives `Ack`.

## 3. SchedulerActor — Local Queue + SinkRef

- [x] 3.1 Refactor `SchedulerActor` in `src/Njord/Pipeline/SchedulerActor.cs`: replace the `RequestPipelineQueue`/`PipelineQueueResponse` handshake with `RequestPipelineSink`/`PipelineSinkResponse`. On receiving the SinkRef, materialize a local `Source.Queue<WeightedTarget>(32, Backpressure).To(sinkRef.Sink).Run(mat)` and store the local queue.
- [x] 3.2 Update `OnScheduledPoll`, `OnRefreshModel`, `OnRefreshLocation` to offer into the local queue (same `OfferAsync` calls, but now on the actor's own queue).
- [x] 3.3 Write `SchedulerActorSinkRefSpec` in `src/Njord.Tests/Pipeline/SchedulerActorSinkRefSpec.cs` — test: actor stashes until SinkRef arrives; after SinkRef, poll timers schedule and targets flow through the SinkRef into the downstream; `OnRefreshLocation` fans out N offers.

## 4. MqttEgressActor — SourceRef Consumer + Delta Logic

- [x] 4.1 Refactor `MqttEgressActor` in `src/Njord/Egress/MqttEgressActor.cs`: add a `RequestPipelineSource` call in `PreStart` (or after internal MergeHub materialization). Stash until `PipelineSourceResponse(SourceRef)` arrives.
- [x] 4.2 On receiving the SourceRef, materialize the egress consumer graph: `sourceRef.Source.Select(BuildPerHorizon).Select(DeltaFilter).SelectMany(→ MqttMessage).RunWith(mergeHubSink, mat)`. Move `StatePayloadBuilder` usage and delta-publish `Dictionary` into this consumer graph.
- [x] 4.3 Remove the `SinkRef<MqttMessage>` materialization for external producers (the `StreamRefs.SinkRef<MqttMessage>().To(hubSink)` block and the `SinkRefMaterialized` message). Keep the internal MergeHub for discovery/availability/tombstone queues and the `SelectAsync(1, transport.SendAsync)` drain.
- [x] 4.4 Handle `Terminated` from PipelineActor: on pipeline restart, re-send `RequestPipelineSource` and rematerialize the consumer graph with a fresh SourceRef. The internal MergeHub continues running.
- [x] 4.5 Write `MqttEgressActorSourceRefSpec` in `src/Njord.Tests/Egress/MqttEgressActorSourceRefSpec.cs` — test: actor requests SourceRef from pipeline; consumer graph maps `FetchOutcome.Success` to `MqttMessage`(s) and publishes via transport; delta logic skips unchanged horizons; pipeline restart triggers re-request.

## 5. Actor Wiring + Cleanup

- [x] 5.1 Update `src/Njord/Program.cs` actor registration: verify startup order doesn't break (PipelineActor now materializes independently). Remove any startup dependencies on egress-first ordering.
- [x] 5.2 Delete dead code: `RequestEgressSink`, `EgressSinkResponse`, `RequestPipelineQueue`, `PipelineQueueResponse`, and any unused `SinkRefMaterialized` message.
- [x] 5.3 Update existing tests in `src/Njord.Tests/` that reference the old handshake messages or mock the old queue-handle pattern. Ensure all existing tests compile and pass.

## 6. Validation

- [x] 6.1 Run full test suite: `dotnet run --project src/Njord.Tests/Njord.Tests.csproj` from `src/` — all tests green.
- [x] 6.2 Run `dotnet build src/Njord.slnx` — no warnings, no errors.
- [x] 6.3 Run `dotnet slopwatch` from repo root — verify no regressions.
